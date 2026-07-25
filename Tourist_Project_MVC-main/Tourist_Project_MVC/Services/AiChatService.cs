using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Services
{
    // Talks to Google's Gemini API (generateContent), grounded on:
    //   1) the model's own general knowledge (Egyptian history, sites, travel tips)
    //   2) the real, bookable Destinations currently in our database (injected
    //      into the system prompt every turn, so the assistant never invents a
    //      destination or an ID that doesn't exist)
    //
    // Extension point for later: if/when you add source documents (the "some
    // data from documents" you mentioned), the cleanest place to plug that in
    // is BuildSystemPrompt below — retrieve the relevant chunks for the
    // user's message (keyword search to start, or embeddings + pgvector once
    // you outgrow that) and append them as an extra "Reference material" block,
    // the same way destinationsBlock is appended today.
    public class AiChatService : IAiChatService
    {
        private const string SaveTripToolName = "save_trip_plan";
        private const string AddDestinationToolName = "add_destination_to_trip";
        private const string RemoveDestinationToolName = "remove_destination_from_trip";
        private const string ReorderDestinationsToolName = "reorder_trip_destinations";

        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly IDestinationRepository _destinationRepo;
        private readonly ITripPlanRepository _tripPlanRepo;
        private readonly IChatSessionRepository _chatSessionRepo;
        private readonly ILogger<AiChatService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public AiChatService(
            HttpClient http,
            IConfiguration config,
            IDestinationRepository destinationRepo,
            ITripPlanRepository tripPlanRepo,
            IChatSessionRepository chatSessionRepo,
            ILogger<AiChatService> logger)
        {
            _http = http;
            _config = config;
            _destinationRepo = destinationRepo;
            _tripPlanRepo = tripPlanRepo;
            _chatSessionRepo = chatSessionRepo;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        private string ApiKey => _config["Gemini:ApiKey"] ?? string.Empty;
        private string Model => _config["Gemini:Model"] ?? "gemini-2.5-flash";

        public async Task<AiChatResponseVM> GetReplyAsync(AiChatRequestVM request, Tourist? tourist, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                _logger.LogWarning("AiChatService called but Gemini:ApiKey is not configured.");
                return new AiChatResponseVM
                {
                    Reply = "The AI assistant isn't configured yet — a Gemini API key is missing on the server. " +
                            "(Developer: set Gemini:ApiKey via 'dotnet user-secrets set Gemini:ApiKey \"...\"'.)"
                };
            }

            var destinations = _destinationRepo.GetAll()
                .Where(d => d.Status == "Active")
                .Select(d => new AiDestinationContext
                {
                    Id = d.Id,
                    Name = d.Name,
                    City = d.City,
                    Category = d.Category,
                    TicketPrice = d.TicketPrice,
                    Rating = d.Rating
                })
                .ToList();

            var touristTrips = tourist != null
                ? _tripPlanRepo.GetAllWithDetails()
                    .Where(t => t.TouristId == tourist.Id)
                    .OrderByDescending(t => t.StartDate)
                    .Take(10)
                    .ToList()
                : new List<TripPlan>();

            // Gemini has no "system" role in `contents` — the system prompt goes in
            // the separate system_instruction field. Roles inside `contents` are
            // "user" and "model" (not "assistant").
            var contents = new List<GeminiContent>();

            // History now arrives as a JSON string — deserialize it back into a list.
            var historyList = JsonSerializer.Deserialize<List<AiChatMessageVM>>(request.History ?? "[]", _jsonOptions) ?? new();
            string? lastRole = null;
            foreach (var turn in historyList.TakeLast(16))
            {
                // Gemini rejects turns with empty text (e.g. image-only messages).
                if (string.IsNullOrWhiteSpace(turn.Content)) continue;

                var role = turn.Role == "assistant" ? "model" : "user";

                // Gemini also rejects consecutive same-role turns — skip duplicates.
                if (role == lastRole) continue;

                contents.Add(new GeminiContent { Role = role, Parts = new List<GeminiPart> { new GeminiPart { Text = turn.Content } } });
                lastRole = role;
            }

            //Edge Case
            // Gemini requires the first turn in `contents` to be "user".
            // If history filtering caused it to start with "model", strip those turns.
            while(contents.Count>0 && contents[0].Role == "model")
            {
                contents.RemoveAt(0);
            }
            // Build parts for the current message: text + any uploaded images + audio.
            var currentParts = new List<GeminiPart>();

            if (!string.IsNullOrWhiteSpace(request.Message))
                currentParts.Add(new GeminiPart { Text = request.Message });

            // Images arrive as two parallel JSON arrays: base64 data and MIME types.
            var imagesBase64 = JsonSerializer.Deserialize<List<string>>(request.ImagesBase64 ?? "[]", _jsonOptions) ?? new();
            var imagesMimeTypes = JsonSerializer.Deserialize<List<string>>(request.ImagesMimeTypes ?? "[]", _jsonOptions) ?? new();
            for (int i = 0; i < imagesBase64.Count; i++)
            {
                currentParts.Add(new GeminiPart
                {
                    InlineData = new GeminiInlineData
                    {
                        MimeType = i < imagesMimeTypes.Count ? imagesMimeTypes[i] : "image/jpeg",
                        Data = imagesBase64[i]
                    }
                });
            }

            // Audio arrives as a single base64 string.
            if (!string.IsNullOrWhiteSpace(request.AudioBase64))
            {
                currentParts.Add(new GeminiPart
                {
                    InlineData = new GeminiInlineData
                    {
                        MimeType = request.AudioMimeType ?? "audio/x-m4a",
                        Data = request.AudioBase64
                    }
                });
            }

            contents.Add(new GeminiContent { Role = "user", Parts = currentParts });


            var payload = new GeminiRequest
            {
                SystemInstruction = new GeminiContent
                {
                    Parts = new List<GeminiPart> { new GeminiPart { Text = BuildSystemPrompt(tourist, destinations, touristTrips) } }
                },
                Contents = contents,
                Tools = new List<GeminiTool> { BuildSaveTripTool(), BuildAddDestinationTool(), BuildRemoveDestinationTool(), BuildReorderDestinationsTool() },
                GenerationConfig = new GeminiGenerationConfig { Temperature = 0.4 }
            };

            GeminiResponse? apiResponse;
            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent";
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
                httpRequest.Headers.Add("x-goog-api-key", ApiKey);
                httpRequest.Content = JsonContent.Create(payload, options: new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                using var httpResponse = await _http.SendAsync(httpRequest, ct);
                var body = await httpResponse.Content.ReadAsStringAsync(ct);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Gemini API error {Status}: {Body}", httpResponse.StatusCode, body);
                    return new AiChatResponseVM
                    {
                        Reply = "Sorry, I couldn't reach the AI service just now. Please try again in a moment."
                    };
                }

                apiResponse = JsonSerializer.Deserialize<GeminiResponse>(body, _jsonOptions);
            }
            catch (TaskCanceledException)
            {
                return new AiChatResponseVM { Reply = "That took too long to answer — please try again." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calling Gemini.");
                return new AiChatResponseVM { Reply = "Something went wrong on our side. Please try again." };
            }

            if (apiResponse?.PromptFeedback?.BlockReason != null)
            {
                return new AiChatResponseVM { Reply = "I can't help with that request. Could you ask something else?" };
            }

            var candidate = apiResponse?.Candidates?.FirstOrDefault();
            var parts = candidate?.Content?.Parts ?? new List<GeminiPart>();

            var functionCallPart = parts.FirstOrDefault(p => p.FunctionCall != null);
            AiChatResponseVM? functionResponse = null;
            if (functionCallPart?.FunctionCall != null)
            {
                var fc = functionCallPart.FunctionCall;
                switch (fc.Name)
                {
                    case SaveTripToolName:
                        return HandleSaveTripToolCall(fc, tourist, destinations);
                    case AddDestinationToolName:
                        functionResponse = HandleAddDestinationToolCall(fc, tourist, destinations);
                        break;
                    case RemoveDestinationToolName:
                        functionResponse = HandleRemoveDestinationToolCall(fc, tourist, destinations);
                        break;
                    case ReorderDestinationsToolName:
                        functionResponse = HandleReorderDestinationsToolCall(fc, tourist, destinations);
                        break;
                }

                if (functionResponse != null)
                {
                    if (tourist != null)
                    {
                        try
                        {
                            await PersistChatAsync(request, functionResponse, tourist);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to persist chat session for tourist {TouristId}", tourist.Id);
                        }
                    }
                    return functionResponse;
                }
            }

            var reply = string.Concat(parts.Where(p => p.Text != null).Select(p => p.Text)).Trim();
            var response = new AiChatResponseVM
            {
                Reply = string.IsNullOrWhiteSpace(reply)
                    ? "I'm not sure how to answer that — could you rephrase?"
                    : reply
            };

            if (tourist != null)
            {
                try
                {
                    await PersistChatAsync(request, response, tourist);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist chat session for tourist {TouristId}", tourist.Id);
                }
            }

            return response;
        }

        private async Task PersistChatAsync(AiChatRequestVM request, AiChatResponseVM response, Tourist tourist)
        {
            ChatSession? session = null;

            if (request.ChatSessionId.HasValue)
            {
                session = await _chatSessionRepo.GetByIdAsync(request.ChatSessionId.Value);
                if (session == null || session.TouristId != tourist.Id)
                {
                    session = null;
                }
            }

            if (session == null)
            {
                session = new ChatSession
                {
                    TouristId = tourist.Id,
                    Title = DeriveTitle(request.Message),
                    MessagesJson = "[]",
                    CreatedDate = DateTime.Now,
                    UpdatedDate = DateTime.Now
                };
                _chatSessionRepo.Add(session);
                _chatSessionRepo.Save();
            }

            var messages = JsonSerializer.Deserialize<List<AiChatMessageVM>>(session.MessagesJson, _jsonOptions) ?? new();
            if (!string.IsNullOrWhiteSpace(request.Message))
            {
                messages.Add(new AiChatMessageVM { Role = "user", Content = request.Message });
            }
            messages.Add(new AiChatMessageVM { Role = "assistant", Content = response.Reply });

            session.MessagesJson = JsonSerializer.Serialize(messages, _jsonOptions);
            session.UpdatedDate = DateTime.Now;
            _chatSessionRepo.Update(session);
            _chatSessionRepo.Save();

            response.ChatSessionId = session.Id;
        }

        private static string DeriveTitle(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "New conversation";

            var trimmed = message.Trim();
            if (trimmed.Length <= 40) return trimmed;
            return trimmed.Substring(0, 40).TrimEnd() + "…";
        }

        private AiChatResponseVM HandleSaveTripToolCall(GeminiFunctionCall functionCall, Tourist? tourist, List<AiDestinationContext> destinations)
        {
            if (tourist == null)
            {
                return new AiChatResponseVM
                {
                    Reply = "I'd love to save that trip for you, but you'll need to sign in first. " +
                            "Log in or create an account, then ask me again and I'll save it to your profile."
                };
            }

            SaveTripArgs? args;
            try
            {
                args = JsonSerializer.Deserialize<SaveTripArgs>(functionCall.Args.GetRawText(), _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Could not parse save_trip_plan arguments: {Args}", functionCall.Args.GetRawText());
                args = null;
            }

            if (args == null)
            {
                return new AiChatResponseVM
                {
                    Reply = "I tried to save the trip but something about the details didn't come through correctly. Could you tell me the plan again?"
                };
            }

            // Keep only destination IDs that are real and active — never trust the model blindly.
            var validIds = destinations.Select(d => d.Id).ToHashSet();
            var chosenIds = args.DestinationIds.Where(id => validIds.Contains(id)).Distinct().ToList();

            if (!chosenIds.Any())
            {
                return new AiChatResponseVM
                {
                    Reply = "I couldn't match that plan to any real destinations in our catalog — could you name the places again (e.g. \"Karnak Temple\", \"Abu Simbel\")?"
                };
            }

            var startDate = ParseDateOrDefault(args.StartDate, DateTime.Today);
            var endDate = ParseDateOrDefault(args.EndDate, startDate.AddDays(3));
            if (endDate < startDate) (startDate, endDate) = (endDate, startDate);

            var title = string.IsNullOrWhiteSpace(args.Title) ? "My AI-Planned Trip" : args.Title.Trim();

            // Mirrors TripController: finalize an existing Draft trip if present,
            // otherwise create a brand-new Active one.
            var draft = _tripPlanRepo.GetDraftTrip(tourist.Id);
            TripPlan trip;

            if (draft != null)
            {
                _tripPlanRepo.RemoveTripDestinations(draft.Id);
                draft.Title = title;
                draft.StartDate = startDate;
                draft.EndDate = endDate;
                draft.Budget = args.Budget;
                draft.Companions = args.Companions;
                draft.Status = "Active";
                draft.TripDestinations.Clear();
                for (var i = 0; i < chosenIds.Count; i++)
                {
                    draft.TripDestinations.Add(new TripDestination
                    {
                        DestinationId = chosenIds[i],
                        Visit_Order = i + 1,
                        ArrivalDate = startDate,
                        DepartureDate = endDate
                    });
                }
                _tripPlanRepo.Update(draft);
                trip = draft;
            }
            else
            {
                trip = new TripPlan
                {
                    Title = title,
                    StartDate = startDate,
                    EndDate = endDate,
                    Budget = args.Budget,
                    Companions = args.Companions,
                    Status = "Active",
                    TouristId = tourist.Id,
                    TripDestinations = chosenIds.Select((id, index) => new TripDestination
                    {
                        DestinationId = id,
                        Visit_Order = index + 1,
                        ArrivalDate = startDate,
                        DepartureDate = endDate
                    }).ToList()
                };
                _tripPlanRepo.Add(trip);
            }

            _tripPlanRepo.Save();

            var names = destinations
                .Where(d => chosenIds.Contains(d.Id))
                .Select(d => d.Name);

            var reply = $"Done! I've saved **{title}** ({startDate:MMM d} – {endDate:MMM d}) with " +
                        $"{chosenIds.Count} stop{(chosenIds.Count == 1 ? "" : "s")}: {string.Join(", ", names)}. " +
                        "You can view or tweak it any time on your Trip page.";

            return new AiChatResponseVM
            {
                Reply = reply,
                TripSaved = true,
                TripPlanId = trip.Id,
                TripPlanTitle = trip.Title
            };
        }

        private AiChatResponseVM HandleAddDestinationToolCall(GeminiFunctionCall functionCall, Tourist? tourist, List<AiDestinationContext> destinations)
        {
            if (tourist == null)
            {
                return new AiChatResponseVM
                {
                    Reply = "I'd love to add that destination, but you'll need to sign in first. " +
                            "Log in or create an account, then ask me again and I'll add it to your trip."
                };
            }

            AddDestinationArgs? args;
            try
            {
                args = JsonSerializer.Deserialize<AddDestinationArgs>(functionCall.Args.GetRawText(), _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Could not parse add_destination_to_trip arguments: {Args}", functionCall.Args.GetRawText());
                args = null;
            }

            if (args == null)
            {
                return new AiChatResponseVM
                {
                    Reply = "I tried to add the destination but something about the details didn't come through correctly. Could you tell me again?"
                };
            }

            var trip = _tripPlanRepo.GetByIdWithDetails(args.TripPlanId);
            if (trip == null)
            {
                return new AiChatResponseVM
                {
                    Reply = "I couldn't find that trip plan. Could you check the plan ID and try again?"
                };
            }

            if (trip.TouristId != tourist.Id)
            {
                return new AiChatResponseVM
                {
                    Reply = "I can't modify that trip plan."
                };
            }

            var validIds = destinations.Select(d => d.Id).ToHashSet();
            if (!validIds.Contains(args.DestinationId))
            {
                return new AiChatResponseVM
                {
                    Reply = "That doesn't match any active destination in our catalog — could you double-check the destination ID?"
                };
            }

            if (trip.TripDestinations.Any(td => td.DestinationId == args.DestinationId))
            {
                return new AiChatResponseVM
                {
                    Reply = "That destination is already included in this trip plan."
                };
            }

            var maxOrder = trip.TripDestinations.Any()
                ? trip.TripDestinations.Max(td => td.Visit_Order)
                : 0;

            var destName = destinations.First(d => d.Id == args.DestinationId).Name;
            var newStop = new TripDestination
            {
                TripPlanId = trip.Id,
                DestinationId = args.DestinationId,
                Visit_Order = maxOrder + 1,
                ArrivalDate = trip.StartDate,
                DepartureDate = trip.EndDate
            };
            _tripPlanRepo.AddStop(newStop);
            _tripPlanRepo.Save();

            var reply = $"Done! I've added **{destName}** to **{trip.Title}** as stop {maxOrder + 1}.";

            return new AiChatResponseVM
            {
                Reply = reply,
                TripSaved = true,
                TripPlanId = trip.Id,
                TripPlanTitle = trip.Title
            };
        }

        private AiChatResponseVM HandleRemoveDestinationToolCall(GeminiFunctionCall functionCall, Tourist? tourist, List<AiDestinationContext> destinations)
        {
            if (tourist == null)
            {
                return new AiChatResponseVM
                {
                    Reply = "I'd love to remove that destination, but you'll need to sign in first. " +
                            "Log in or create an account, then ask me again and I'll remove it from your trip."
                };
            }

            RemoveDestinationArgs? args;
            try
            {
                args = JsonSerializer.Deserialize<RemoveDestinationArgs>(functionCall.Args.GetRawText(), _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Could not parse remove_destination_from_trip arguments: {Args}", functionCall.Args.GetRawText());
                args = null;
            }

            if (args == null)
            {
                return new AiChatResponseVM
                {
                    Reply = "I tried to remove the destination but something about the details didn't come through correctly. Could you tell me again?"
                };
            }

            var trip = _tripPlanRepo.GetByIdWithDetails(args.TripPlanId);
            if (trip == null)
            {
                return new AiChatResponseVM
                {
                    Reply = "I couldn't find that trip plan. Could you check the plan ID and try again?"
                };
            }

            if (trip.TouristId != tourist.Id)
            {
                return new AiChatResponseVM
                {
                    Reply = "I can't modify that trip plan."
                };
            }

            var existingStop = trip.TripDestinations.FirstOrDefault(td => td.DestinationId == args.DestinationId);
            if (existingStop == null)
            {
                return new AiChatResponseVM
                {
                    Reply = "That destination isn't part of this trip plan."
                };
            }

            var destName = destinations.FirstOrDefault(d => d.Id == args.DestinationId)?.Name ?? "that destination";
            _tripPlanRepo.RemoveStop(existingStop.Id);

            var remainingStops = trip.TripDestinations
                .Where(td => td.Id != existingStop.Id)
                .OrderBy(td => td.Visit_Order)
                .ToList();

            for (var i = 0; i < remainingStops.Count; i++)
            {
                remainingStops[i].Visit_Order = i + 1;
                _tripPlanRepo.UpdateStop(remainingStops[i]);
            }

            _tripPlanRepo.Save();

            var reply = $"Done! I've removed **{destName}** from **{trip.Title}**. " +
                        $"The remaining stops have been renumbered 1..{remainingStops.Count}.";

            return new AiChatResponseVM
            {
                Reply = reply,
                TripSaved = true,
                TripPlanId = trip.Id,
                TripPlanTitle = trip.Title
            };
        }

        private AiChatResponseVM HandleReorderDestinationsToolCall(GeminiFunctionCall functionCall, Tourist? tourist, List<AiDestinationContext> destinations)
        {
            if (tourist == null)
            {
                return new AiChatResponseVM
                {
                    Reply = "I'd love to reorder your trip, but you'll need to sign in first. " +
                            "Log in or create an account, then ask me again and I'll reorder it."
                };
            }

            ReorderDestinationsArgs? args;
            try
            {
                args = JsonSerializer.Deserialize<ReorderDestinationsArgs>(functionCall.Args.GetRawText(), _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Could not parse reorder_trip_destinations arguments: {Args}", functionCall.Args.GetRawText());
                args = null;
            }

            if (args == null)
            {
                return new AiChatResponseVM
                {
                    Reply = "I tried to reorder the destinations but something about the details didn't come through correctly. Could you tell me again?"
                };
            }

            var trip = _tripPlanRepo.GetByIdWithDetails(args.TripPlanId);
            if (trip == null)
            {
                return new AiChatResponseVM
                {
                    Reply = "I couldn't find that trip plan. Could you check the plan ID and try again?"
                };
            }

            if (trip.TouristId != tourist.Id)
            {
                return new AiChatResponseVM
                {
                    Reply = "I can't modify that trip plan."
                };
            }

            var currentIds = trip.TripDestinations.Select(td => td.DestinationId).ToHashSet();
            var newIds = args.DestinationIds.ToHashSet();

            if (currentIds.Count != newIds.Count || !currentIds.SetEquals(newIds) || args.DestinationIds.Count != args.DestinationIds.Distinct().Count())
            {
                return new AiChatResponseVM
                {
                    Reply = "The destination list you provided doesn't match the current stops in this trip. " +
                            "Please confirm the full new order by listing all destination IDs exactly once."
                };
            }

            for (var i = 0; i < args.DestinationIds.Count; i++)
            {
                var stop = trip.TripDestinations.First(td => td.DestinationId == args.DestinationIds[i]);
                stop.Visit_Order = i + 1;
                _tripPlanRepo.UpdateStop(stop);
            }

            _tripPlanRepo.Save();

            var reply = $"Done! I've reordered **{trip.Title}** to: " +
                        $"{string.Join(" → ", args.DestinationIds.Select(id => destinations.First(d => d.Id == id).Name))}.";

            return new AiChatResponseVM
            {
                Reply = reply,
                TripSaved = true,
                TripPlanId = trip.Id,
                TripPlanTitle = trip.Title
            };
        }

        private static DateTime ParseDateOrDefault(string? value, DateTime fallback)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed.Date;
            }
            return fallback.Date;
        }

        private static string BuildSystemPrompt(Tourist? tourist, List<AiDestinationContext> destinations, List<TripPlan> touristTrips)
        {
            var destinationsBlock = string.Join("\n", destinations.Select(d =>
                $"- id={d.Id} | {d.Name} | {d.City} | {d.Category ?? "General"} | " +
                $"price={(d.TicketPrice.HasValue ? d.TicketPrice.Value.ToString("0.##") : "free")} EGP | " +
                $"rating={(d.Rating.HasValue ? d.Rating.Value.ToString("0.0") : "n/a")}"));

            var touristLine = tourist != null
                ? $"The signed-in tourist is named {tourist.Name}."
                : "This visitor is not signed in — you can chat, but you cannot save a trip for them until they log in.";

            var tripsBlock = touristTrips.Any()
                ? string.Join("\n", touristTrips.Select(t =>
                    $"- plan_id={t.Id} | \"{t.Title}\" | status={t.Status} | {t.StartDate:yyyy-MM-dd} to {t.EndDate:yyyy-MM-dd} | stops: [{string.Join(", ", t.TripDestinations.OrderBy(td => td.Visit_Order).Select(td => $"order{td.Visit_Order}: id={td.DestinationId} {td.Destination?.Name ?? "unknown"}"))}]"))
                : "This tourist has no saved trip plans yet.";

            return $"""
                You are the EGYXPLORE Assistant, a friendly and knowledgeable travel guide embedded in a
                tourism website about Egypt. You have three jobs:

                1. Answer questions about the history of Egypt (Ancient Egyptian civilization, pharaohs,
                   dynasties, monuments, temples, mythology, and more recent history too) and about
                   historical/touristic locations in Egypt. Be accurate, engaging, and reasonably concise
                   unless the user asks for depth.

                2. Help the user plan a trip: suggest an itinerary using ONLY the real destinations listed
                   below (never invent a place or an ID). Ask about interests, trip length, budget, or
                   number of travelers if useful, but don't interrogate the user with too many questions —
                   propose a solid plan and refine it based on feedback. You can also call the
                   add_destination_to_trip, remove_destination_from_trip, and reorder_trip_destinations
                   tools to modify existing trip plans.

                3. Help the user modify their existing trip plans listed below — add a destination, remove one, or reorder the stops — using the destination IDs and plan IDs from that list. Only call an edit tool once the user has clearly confirmed which plan and what change they want. If the user has multiple trips and hasn't specified which one, ask them to clarify instead of guessing.

                {touristLine}

                When the user is happy with a plan and confirms they want it saved (phrases like "save it",
                "book this", "yes let's do that", "add this to my trip"), call the `save_trip_plan` tool with
                the destination IDs from the list below, in the order they'll be visited. Only call the tool
                once you and the user have actually agreed on a concrete set of destinations and rough dates —
                don't call it just because a place was mentioned in passing.

                Available destinations (id | name | city | category | ticket price | rating):
                {destinationsBlock}

                Existing trip plans:
                {tripsBlock}

                Today's date is {DateTime.Today:yyyy-MM-dd}. If the user doesn't give exact dates, choose
                sensible ones relative to today. Keep replies in plain, warm language — this is a chat widget,
                not a report, so avoid heavy markdown or long bullet lists unless it truly helps.
                """;
        }

        private static GeminiTool BuildSaveTripTool()
        {
            return new GeminiTool
            {
                FunctionDeclarations = new List<GeminiFunctionDeclaration>
                {
                    new GeminiFunctionDeclaration
                    {
                        Name = SaveTripToolName,
                        Description = "Save a confirmed trip plan for the signed-in tourist using real destination IDs.",
                        Parameters = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                title = new { type = "STRING", description = "Short, friendly title for the trip." },
                                start_date = new { type = "STRING", description = "ISO date (YYYY-MM-DD) the trip starts." },
                                end_date = new { type = "STRING", description = "ISO date (YYYY-MM-DD) the trip ends." },
                                budget = new { type = "NUMBER", description = "Optional total budget in EGP." },
                                companions = new { type = "INTEGER", description = "Optional number of travelers." },
                                destination_ids = new
                                {
                                    type = "ARRAY",
                                    items = new { type = "INTEGER" },
                                    description = "IDs of the chosen destinations, from the provided list, in visit order."
                                }
                            },
                            required = new[] { "title", "start_date", "end_date", "destination_ids" }
                        }
                    }
                }
            };
        }

        private static GeminiTool BuildAddDestinationTool()
        {
            return new GeminiTool
            {
                FunctionDeclarations = new List<GeminiFunctionDeclaration>
                {
                    new GeminiFunctionDeclaration
                    {
                        Name = AddDestinationToolName,
                        Description = "Add a destination to an existing trip plan by its plan ID and destination ID.",
                        Parameters = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                trip_plan_id = new { type = "INTEGER", description = "The ID of the trip plan to add the destination to." },
                                destination_id = new { type = "INTEGER", description = "The ID of the destination to add, from the available destinations list." }
                            },
                            required = new[] { "trip_plan_id", "destination_id" }
                        }
                    }
                }
            };
        }

        private static GeminiTool BuildRemoveDestinationTool()
        {
            return new GeminiTool
            {
                FunctionDeclarations = new List<GeminiFunctionDeclaration>
                {
                    new GeminiFunctionDeclaration
                    {
                        Name = RemoveDestinationToolName,
                        Description = "Remove a destination from an existing trip plan by its plan ID and destination ID.",
                        Parameters = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                trip_plan_id = new { type = "INTEGER", description = "The ID of the trip plan to remove the destination from." },
                                destination_id = new { type = "INTEGER", description = "The ID of the destination to remove." }
                            },
                            required = new[] { "trip_plan_id", "destination_id" }
                        }
                    }
                }
            };
        }

        private static GeminiTool BuildReorderDestinationsTool()
        {
            return new GeminiTool
            {
                FunctionDeclarations = new List<GeminiFunctionDeclaration>
                {
                    new GeminiFunctionDeclaration
                    {
                        Name = ReorderDestinationsToolName,
                        Description = "Reorder the destinations in an existing trip plan by providing a new ordered list of destination IDs.",
                        Parameters = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                trip_plan_id = new { type = "INTEGER", description = "The ID of the trip plan to reorder." },
                                destination_ids = new
                                {
                                    type = "ARRAY",
                                    items = new { type = "INTEGER" },
                                    description = "The destination IDs in the new desired visit order. Must include every destination currently in the trip exactly once."
                                }
                            },
                            required = new[] { "trip_plan_id", "destination_ids" }
                        }
                    }
                }
            };
        }
    }
}
