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

        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly IDestinationRepository _destinationRepo;
        private readonly ITripPlanRepository _tripPlanRepo;
        private readonly ILogger<AiChatService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public AiChatService(
            HttpClient http,
            IConfiguration config,
            IDestinationRepository destinationRepo,
            ITripPlanRepository tripPlanRepo,
            ILogger<AiChatService> logger)
        {
            _http = http;
            _config = config;
            _destinationRepo = destinationRepo;
            _tripPlanRepo = tripPlanRepo;
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

            // Gemini has no "system" role in `contents` — the system prompt goes in
            // the separate system_instruction field. Roles inside `contents` are
            // "user" and "model" (not "assistant").
            var contents = new List<GeminiContent>();

            foreach (var turn in request.History.TakeLast(16))
            {
                var role = turn.Role == "assistant" ? "model" : "user";
                contents.Add(new GeminiContent { Role = role, Parts = new List<GeminiPart> { new GeminiPart { Text = turn.Content } } });
            }

            contents.Add(new GeminiContent { Role = "user", Parts = new List<GeminiPart> { new GeminiPart { Text = request.Message } } });

            var payload = new GeminiRequest
            {
                SystemInstruction = new GeminiContent
                {
                    Parts = new List<GeminiPart> { new GeminiPart { Text = BuildSystemPrompt(tourist, destinations) } }
                },
                Contents = contents,
                Tools = new List<GeminiTool> { BuildSaveTripTool() },
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

            var functionCallPart = parts.FirstOrDefault(p => p.FunctionCall != null && p.FunctionCall.Name == SaveTripToolName);
            if (functionCallPart?.FunctionCall != null)
            {
                return HandleSaveTripToolCall(functionCallPart.FunctionCall, tourist, destinations);
            }

            var reply = string.Concat(parts.Where(p => p.Text != null).Select(p => p.Text)).Trim();
            return new AiChatResponseVM
            {
                Reply = string.IsNullOrWhiteSpace(reply)
                    ? "I'm not sure how to answer that — could you rephrase?"
                    : reply
            };
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

        private static DateTime ParseDateOrDefault(string? value, DateTime fallback)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed.Date;
            }
            return fallback.Date;
        }

        private static string BuildSystemPrompt(Tourist? tourist, List<AiDestinationContext> destinations)
        {
            var destinationsBlock = string.Join("\n", destinations.Select(d =>
                $"- id={d.Id} | {d.Name} | {d.City} | {d.Category ?? "General"} | " +
                $"price={(d.TicketPrice.HasValue ? d.TicketPrice.Value.ToString("0.##") : "free")} EGP | " +
                $"rating={(d.Rating.HasValue ? d.Rating.Value.ToString("0.0") : "n/a")}"));

            var touristLine = tourist != null
                ? $"The signed-in tourist is named {tourist.Name}."
                : "This visitor is not signed in — you can chat, but you cannot save a trip for them until they log in.";

            return $"""
                You are the EGYXPLORE Assistant, a friendly and knowledgeable travel guide embedded in a
                tourism website about Egypt. You have two jobs:

                1. Answer questions about the history of Egypt (Ancient Egyptian civilization, pharaohs,
                   dynasties, monuments, temples, mythology, and more recent history too) and about
                   historical/touristic locations in Egypt. Be accurate, engaging, and reasonably concise
                   unless the user asks for depth.

                2. Help the user plan a trip: suggest an itinerary using ONLY the real destinations listed
                   below (never invent a place or an ID). Ask about interests, trip length, budget, or
                   number of travelers if useful, but don't interrogate the user with too many questions —
                   propose a solid plan and refine it based on feedback.

                {touristLine}

                When the user is happy with a plan and confirms they want it saved (phrases like "save it",
                "book this", "yes let's do that", "add this to my trip"), call the `save_trip_plan` tool with
                the destination IDs from the list below, in the order they'll be visited. Only call the tool
                once you and the user have actually agreed on a concrete set of destinations and rough dates —
                don't call it just because a place was mentioned in passing.

                Available destinations (id | name | city | category | ticket price | rating):
                {destinationsBlock}

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
    }
}
