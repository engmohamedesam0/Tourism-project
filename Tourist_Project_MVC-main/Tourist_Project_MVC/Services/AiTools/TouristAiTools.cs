using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services.AiAgent;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Services.AiTools
{
    /// <summary>
    /// Tourist ("User" role) tools. Every operation is scoped to the Tourist
    /// record resolved server-side from the authenticated identity — a Tourist
    /// ID or trip ID supplied by the user/model is NEVER used directly; the
    /// backend always derives ownership from the current user.
    /// </summary>
    public class TouristAiTools
    {
        private static readonly string[] TouristRole = { AiIdentityContext.RoleTourist };

        private readonly ITripPlanRepository _tripPlanRepo;
        private readonly ITouristRepository _touristRepo;
        private readonly IDestinationRepository _destinationRepo;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IGamificationService _gamificationService;

        public TouristAiTools(
            ITripPlanRepository tripPlanRepo,
            ITouristRepository touristRepo,
            IDestinationRepository destinationRepo,
            UserManager<ApplicationUser> userManager,
            IGamificationService gamificationService)
        {
            _tripPlanRepo = tripPlanRepo;
            _touristRepo = touristRepo;
            _destinationRepo = destinationRepo;
            _userManager = userManager;
            _gamificationService = gamificationService;
        }

        public List<AiToolDefinition> Build() => new()
        {
            CreateTrip(),
            GetMyTrips(),
            UpdateTrip(),
            DeleteTrip(),
            AddDestinationToTrip(),
            RemoveDestinationFromTrip(),
            ReorderTripDestinations(),
            GetDestinationPhotos(),
            GetMyProfile(),
            UpdateMyProfile(),
            GetRecommendedDestinations()
        };

        // ============================================================
        // create_trip
        // ============================================================
        private AiToolDefinition CreateTrip() => new()
        {
            Name = "create_trip",
            Description = "Create a NEW trip plan for the signed-in tourist using real destination IDs from the catalog. " +
                          "Required: title, start_date (YYYY-MM-DD), end_date (YYYY-MM-DD), destination_ids (visit order). " +
                          "If any required detail is missing, ask the user for it BEFORE calling this tool. " +
                          "The system will ask the user to confirm before the trip is actually saved.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new
                {
                    title = new { type = "STRING", description = "Short, friendly trip title." },
                    start_date = new { type = "STRING", description = "ISO date (YYYY-MM-DD) the trip starts." },
                    end_date = new { type = "STRING", description = "ISO date (YYYY-MM-DD) the trip ends." },
                    budget = new { type = "NUMBER", description = "Optional total budget in EGP." },
                    companions = new { type = "INTEGER", description = "Optional number of travelers." },
                    destination_ids = new
                    {
                        type = "ARRAY",
                        items = new { type = "INTEGER" },
                        description = "IDs of the chosen destinations (from the catalog), in visit order."
                    }
                },
                required = new[] { "title", "start_date", "end_date", "destination_ids" }
            },
            Roles = TouristRole,
            RequiresConfirmation = true,
            ExecuteAsync = async (args, context, ct) =>
            {
                var tourist = context.Identity.Tourist;
                if (tourist == null)
                    return Fail("You'll need to sign in as a tourist before I can create a trip for you.");

                var parsed = AiToolsCommon.ParseArgs<CreateTripArgs>(args);
                if (parsed == null || !parsed.DestinationIds.Any())
                    return Fail("I couldn't understand the trip details. Could you name the places you'd like to visit again?");

                var validIds = ActiveDestinationIds();
                var chosenIds = parsed.DestinationIds.Where(validIds.Contains).Distinct().ToList();
                if (!chosenIds.Any())
                    return Fail("I couldn't match that plan to any real destinations in our catalog — could you name the places again (e.g. \"Karnak Temple\", \"Abu Simbel\")?");

                var start = AiToolsCommon.ParseDateOrDefault(parsed.StartDate, DateTime.Today);
                var end = AiToolsCommon.ParseDateOrDefault(parsed.EndDate, start.AddDays(3));
                if (end < start) (start, end) = (end, start);

                var title = string.IsNullOrWhiteSpace(parsed.Title) ? "My AI-Planned Trip" : parsed.Title.Trim();
                var names = _destinationRepo.GetAll().Where(d => chosenIds.Contains(d.Id)).Select(d => d.Name).ToList();

                if (context.IsPreview)
                {
                    var summary =
                        $"I'm ready to create this trip:\n\n" +
                        $"Destination{(names.Count > 1 ? "s" : "")}: {string.Join(", ", names)}\n" +
                        $"Duration: {AiToolsCommon.DurationLabel(start, end)}\n" +
                        $"Start Date: {AiToolsCommon.ShortDate(start)}\n" +
                        $"End Date: {AiToolsCommon.ShortDate(end)}\n" +
                        (parsed.Budget.HasValue ? $"Budget: {parsed.Budget.Value:0.##} EGP\n" : "") +
                        (parsed.Companions.HasValue ? $"Travelers: {parsed.Companions.Value}\n" : "");
                    return Ok(summary.TrimEnd());
                }

                // Mirrors TripController.Create / AiChatService.save_trip_plan:
                // finalize an existing Draft trip if present, else create a new Active one.
                var draft = _tripPlanRepo.GetDraftTrip(tourist.Id);
                TripPlan trip;
                if (draft != null)
                {
                    _tripPlanRepo.RemoveTripDestinations(draft.Id);
                    draft.Title = title;
                    draft.StartDate = start;
                    draft.EndDate = end;
                    draft.Budget = parsed.Budget;
                    draft.Companions = parsed.Companions;
                    draft.Status = "Active";
                    draft.TripDestinations.Clear();
                    for (var i = 0; i < chosenIds.Count; i++)
                    {
                        draft.TripDestinations.Add(new TripDestination
                        {
                            DestinationId = chosenIds[i],
                            Visit_Order = i + 1,
                            ArrivalDate = start,
                            DepartureDate = end
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
                        StartDate = start,
                        EndDate = end,
                        Budget = parsed.Budget,
                        Companions = parsed.Companions,
                        Status = "Active",
                        TouristId = tourist.Id,
                        TripDestinations = chosenIds.Select((id, index) => new TripDestination
                        {
                            DestinationId = id,
                            Visit_Order = index + 1,
                            ArrivalDate = start,
                            DepartureDate = end
                        }).ToList()
                    };
                    _tripPlanRepo.Add(trip);
                }
                _tripPlanRepo.Save();

                var reply = $"Done! Your trip to **{string.Join(", ", names.Take(3))}** has been created successfully " +
                            $"({AiToolsCommon.ShortDate(start)} – {AiToolsCommon.ShortDate(end)}). " +
                            "You can view or tweak it any time on your Trip page.";

                return Ok(reply, new AiTripActionData { TripPlanId = trip.Id, TripPlanTitle = trip.Title });
            }
        };

        // ============================================================
        // get_my_trips
        // ============================================================
        private AiToolDefinition GetMyTrips() => new()
        {
            Name = "get_my_trips",
            Description = "List the signed-in tourist's OWN trip plans with their destinations, dates and status. " +
                          "Use when the user asks to see their trips, itinerary, or plans.",
            Parameters = new { type = "OBJECT", properties = new { } },
            Roles = TouristRole,
            ExecuteAsync = (args, context, ct) =>
            {
                var tourist = context.Identity.Tourist;
                if (tourist == null)
                    return Task.FromResult(Fail("You'll need to sign in first."));

                var trips = _tripPlanRepo.GetAllWithDetails()
                    .Where(t => t.TouristId == tourist.Id)
                    .OrderByDescending(t => t.StartDate)
                    .ToList();

                var items = trips.Select(t => new
                {
                    trip_id = t.Id,
                    title = t.Title,
                    status = t.Status,
                    start_date = t.StartDate.ToString("yyyy-MM-dd"),
                    end_date = t.EndDate.ToString("yyyy-MM-dd"),
                    budget = t.Budget,
                    companions = t.Companions,
                    destinations = t.TripDestinations.OrderBy(td => td.Visit_Order)
                        .Select(td => new { order = td.Visit_Order, name = td.Destination?.Name, destination_id = td.DestinationId })
                        .ToList()
                }).ToList();

                var message = trips.Count == 0
                    ? "You don't have any trips yet. I'd be happy to help you plan one!"
                    : $"You have {trips.Count} trip{(trips.Count == 1 ? "" : "s")}:\n" +
                      string.Join("\n\n", trips.Select(t =>
                          $"• **{t.Title}** (id={t.Id}, {t.Status}) — {AiToolsCommon.ShortDate(t.StartDate)} to {AiToolsCommon.ShortDate(t.EndDate)}" +
                          (t.Budget.HasValue ? $", budget {t.Budget.Value:0.##} EGP" : "") +
                          (t.TripDestinations.Any()
                              ? $"\n   Stops: {string.Join(" → ", t.TripDestinations.OrderBy(td => td.Visit_Order).Select(td => td.Destination?.Name ?? "unknown"))}"
                              : "")));

                return Task.FromResult(Ok(message, new { trips = items }));
            }
        };

        // ============================================================
        // update_trip
        // ============================================================
        private AiToolDefinition UpdateTrip() => new()
        {
            Name = "update_trip",
            Description = "Update an EXISTING trip plan of the signed-in tourist (trip_id from get_my_trips). " +
                          "All fields optional; provide only what changes. If destination_ids is provided, the trip's stops are replaced by that exact list. " +
                          "The system will ask the user to confirm before saving.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new
                {
                    trip_id = new { type = "INTEGER", description = "The trip's ID (from get_my_trips)." },
                    title = new { type = "STRING", description = "Optional new title." },
                    start_date = new { type = "STRING", description = "Optional new start date (YYYY-MM-DD)." },
                    end_date = new { type = "STRING", description = "Optional new end date (YYYY-MM-DD)." },
                    budget = new { type = "NUMBER", description = "Optional new budget in EGP." },
                    companions = new { type = "INTEGER", description = "Optional new traveler count." },
                    destination_ids = new
                    {
                        type = "ARRAY",
                        items = new { type = "INTEGER" },
                        description = "Optional replacement stop list (real destination IDs, in visit order)."
                    }
                },
                required = new[] { "trip_id" }
            },
            Roles = TouristRole,
            RequiresConfirmation = true,
            ExecuteAsync = (args, context, ct) =>
            {
                var tourist = context.Identity.Tourist;
                if (tourist == null)
                    return Task.FromResult(Fail("You'll need to sign in first."));

                var parsed = AiToolsCommon.ParseArgs<UpdateTripArgs>(args);
                if (parsed == null || parsed.TripId <= 0)
                    return Task.FromResult(Fail("I need a valid trip to update."));

                var trip = _tripPlanRepo.GetByIdWithDetails(parsed.TripId);
                if (trip == null || trip.TouristId != tourist.Id)
                    return Task.FromResult(Fail("I couldn't find that trip on your account."));

                var newTitle = string.IsNullOrWhiteSpace(parsed.Title) ? trip.Title : parsed.Title.Trim();
                var newStart = string.IsNullOrWhiteSpace(parsed.StartDate) ? trip.StartDate : AiToolsCommon.ParseDateOrDefault(parsed.StartDate, trip.StartDate);
                var newEnd = string.IsNullOrWhiteSpace(parsed.EndDate) ? trip.EndDate : AiToolsCommon.ParseDateOrDefault(parsed.EndDate, newStart.AddDays(3));
                if (newEnd < newStart) (newStart, newEnd) = (newEnd, newStart);

                if (context.IsPreview)
                {
                    var summary = $"I'm ready to update your trip **{trip.Title}**:\n" +
                                  (string.IsNullOrWhiteSpace(parsed.Title) ? "" : $"- New title: {newTitle}\n") +
                                  (string.IsNullOrWhiteSpace(parsed.StartDate) ? "" : $"- New start: {AiToolsCommon.ShortDate(newStart)}\n") +
                                  (string.IsNullOrWhiteSpace(parsed.EndDate) ? "" : $"- New end: {AiToolsCommon.ShortDate(newEnd)}\n") +
                                  (parsed.Budget.HasValue ? $"- New budget: {parsed.Budget.Value:0.##} EGP\n" : "") +
                                  (parsed.Companions.HasValue ? $"- Travelers: {parsed.Companions.Value}\n" : "") +
                                  (parsed.DestinationIds != null
                                      ? $"- Stops replaced with: {string.Join(", ", _destinationRepo.GetAll().Where(d => parsed.DestinationIds.Contains(d.Id)).Select(d => d.Name))}\n"
                                      : "");
                    return Task.FromResult(Ok(summary.TrimEnd()));
                }

                trip.Title = newTitle;
                trip.StartDate = newStart;
                trip.EndDate = newEnd;
                if (parsed.Budget.HasValue) trip.Budget = parsed.Budget;
                if (parsed.Companions.HasValue) trip.Companions = parsed.Companions;

                if (parsed.DestinationIds != null)
                {
                    var validIds = ActiveDestinationIds();
                    var chosen = parsed.DestinationIds.Where(validIds.Contains).Distinct().ToList();
                    if (!chosen.Any())
                        return Task.FromResult(Fail("None of the replacement destinations matched our catalog."));
                    _tripPlanRepo.RemoveTripDestinations(trip.Id);
                    trip.TripDestinations.Clear();
                    for (var i = 0; i < chosen.Count; i++)
                    {
                        trip.TripDestinations.Add(new TripDestination
                        {
                            DestinationId = chosen[i],
                            Visit_Order = i + 1,
                            ArrivalDate = newStart,
                            DepartureDate = newEnd
                        });
                    }
                }

                _tripPlanRepo.Update(trip);
                _tripPlanRepo.Save();

                var reply = $"Done! Your trip **{trip.Title}** has been updated successfully.";
                return Task.FromResult(Ok(reply, new AiTripActionData { TripPlanId = trip.Id, TripPlanTitle = trip.Title }));
            }
        };

        // ============================================================
        // delete_trip
        // ============================================================
        private AiToolDefinition DeleteTrip() => new()
        {
            Name = "delete_trip",
            Description = "Delete one of the signed-in tourist's OWN trip plans (trip_id from get_my_trips). " +
                          "Destructive — the system always asks for explicit confirmation first.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new { trip_id = new { type = "INTEGER", description = "The trip's ID (from get_my_trips)." } },
                required = new[] { "trip_id" }
            },
            Roles = TouristRole,
            RequiresConfirmation = true,
            ExecuteAsync = (args, context, ct) =>
            {
                var tourist = context.Identity.Tourist;
                if (tourist == null)
                    return Task.FromResult(Fail("You'll need to sign in first."));

                var parsed = AiToolsCommon.ParseArgs<TripIdArgs>(args);
                if (parsed == null || parsed.TripId <= 0)
                    return Task.FromResult(Fail("I need a valid trip to delete."));

                var trip = _tripPlanRepo.GetByIdWithDetails(parsed.TripId);
                if (trip == null || trip.TouristId != tourist.Id)
                    return Task.FromResult(Fail("I couldn't find that trip on your account."));

                if (context.IsPreview)
                {
                    var stops = trip.TripDestinations.Any()
                        ? string.Join(", ", trip.TripDestinations.OrderBy(td => td.Visit_Order).Select(td => td.Destination?.Name ?? "unknown"))
                        : "no stops";
                    return Task.FromResult(Ok(
                        $"I found your trip **{trip.Title}** scheduled for {AiToolsCommon.ShortDate(trip.StartDate)} to {AiToolsCommon.ShortDate(trip.EndDate)} " +
                        $"({stops}). Are you sure you want to delete it?"));
                }

                _tripPlanRepo.RemoveTripDestinations(trip.Id);
                _tripPlanRepo.Delete(trip.Id);
                _tripPlanRepo.Save();

                return Task.FromResult(Ok($"Done! Your trip **{trip.Title}** has been deleted."));
            }
        };

        // ============================================================
        // add_destination_to_trip / remove_destination_from_trip / reorder
        // ============================================================
        private AiToolDefinition AddDestinationToTrip() => new()
        {
            Name = "add_destination_to_trip",
            Description = "Add a destination to an existing trip plan of the signed-in tourist (trip_plan_id + destination_id from the catalog).",
            Parameters = new
            {
                type = "OBJECT",
                properties = new
                {
                    trip_plan_id = new { type = "INTEGER", description = "The trip's ID." },
                    destination_id = new { type = "INTEGER", description = "The destination's ID." }
                },
                required = new[] { "trip_plan_id", "destination_id" }
            },
            Roles = TouristRole,
            ExecuteAsync = (args, context, ct) =>
            {
                var tourist = context.Identity.Tourist;
                if (tourist == null)
                    return Task.FromResult(Fail("You'll need to sign in first."));

                var parsed = AiToolsCommon.ParseArgs<AddDestinationArgs>(args);
                if (parsed == null)
                    return Task.FromResult(Fail("I couldn't understand that request — could you tell me again?"));

                var trip = _tripPlanRepo.GetByIdWithDetails(parsed.TripPlanId);
                if (trip == null || trip.TouristId != tourist.Id)
                    return Task.FromResult(Fail("I can't modify that trip plan."));

                var dest = _destinationRepo.GetById(parsed.DestinationId);
                if (dest == null || dest.Status != "Active")
                    return Task.FromResult(Fail("That doesn't match any active destination in our catalog."));

                if (trip.TripDestinations.Any(td => td.DestinationId == parsed.DestinationId))
                    return Task.FromResult(Fail("That destination is already included in this trip plan."));

                var maxOrder = trip.TripDestinations.Any() ? trip.TripDestinations.Max(td => td.Visit_Order) : 0;
                _tripPlanRepo.AddStop(new TripDestination
                {
                    TripPlanId = trip.Id,
                    DestinationId = parsed.DestinationId,
                    Visit_Order = maxOrder + 1,
                    ArrivalDate = trip.StartDate,
                    DepartureDate = trip.EndDate
                });
                _tripPlanRepo.Save();

                var reply = $"Done! I've added **{dest.Name}** to **{trip.Title}** as stop {maxOrder + 1}.";
                return Task.FromResult(Ok(reply, new AiTripActionData { TripPlanId = trip.Id, TripPlanTitle = trip.Title }));
            }
        };

        private AiToolDefinition RemoveDestinationFromTrip() => new()
        {
            Name = "remove_destination_from_trip",
            Description = "Remove a destination from an existing trip plan of the signed-in tourist (trip_plan_id + destination_id).",
            Parameters = new
            {
                type = "OBJECT",
                properties = new
                {
                    trip_plan_id = new { type = "INTEGER", description = "The trip's ID." },
                    destination_id = new { type = "INTEGER", description = "The destination's ID." }
                },
                required = new[] { "trip_plan_id", "destination_id" }
            },
            Roles = TouristRole,
            ExecuteAsync = (args, context, ct) =>
            {
                var tourist = context.Identity.Tourist;
                if (tourist == null)
                    return Task.FromResult(Fail("You'll need to sign in first."));

                var parsed = AiToolsCommon.ParseArgs<RemoveDestinationArgs>(args);
                if (parsed == null)
                    return Task.FromResult(Fail("I couldn't understand that request — could you tell me again?"));

                var trip = _tripPlanRepo.GetByIdWithDetails(parsed.TripPlanId);
                if (trip == null || trip.TouristId != tourist.Id)
                    return Task.FromResult(Fail("I can't modify that trip plan."));

                var existingStop = trip.TripDestinations.FirstOrDefault(td => td.DestinationId == parsed.DestinationId);
                if (existingStop == null)
                    return Task.FromResult(Fail("That destination isn't part of this trip plan."));

                var destName = _destinationRepo.GetById(parsed.DestinationId)?.Name ?? "that destination";
                _tripPlanRepo.RemoveStop(existingStop.Id);

                var remaining = trip.TripDestinations
                    .Where(td => td.Id != existingStop.Id)
                    .OrderBy(td => td.Visit_Order)
                    .ToList();
                for (var i = 0; i < remaining.Count; i++)
                {
                    remaining[i].Visit_Order = i + 1;
                    _tripPlanRepo.UpdateStop(remaining[i]);
                }
                _tripPlanRepo.Save();

                var reply = $"Done! I've removed **{destName}** from **{trip.Title}**. " +
                            $"The remaining stops have been renumbered 1..{remaining.Count}.";
                return Task.FromResult(Ok(reply, new AiTripActionData { TripPlanId = trip.Id, TripPlanTitle = trip.Title }));
            }
        };

        private AiToolDefinition ReorderTripDestinations() => new()
        {
            Name = "reorder_trip_destinations",
            Description = "Reorder the stops of an existing trip plan of the signed-in tourist by providing the full new list of destination IDs (same set, new order).",
            Parameters = new
            {
                type = "OBJECT",
                properties = new
                {
                    trip_plan_id = new { type = "INTEGER", description = "The trip's ID." },
                    destination_ids = new
                    {
                        type = "ARRAY",
                        items = new { type = "INTEGER" },
                        description = "All destination IDs currently in the trip, in the new desired order."
                    }
                },
                required = new[] { "trip_plan_id", "destination_ids" }
            },
            Roles = TouristRole,
            ExecuteAsync = (args, context, ct) =>
            {
                var tourist = context.Identity.Tourist;
                if (tourist == null)
                    return Task.FromResult(Fail("You'll need to sign in first."));

                var parsed = AiToolsCommon.ParseArgs<ReorderDestinationsArgs>(args);
                if (parsed == null)
                    return Task.FromResult(Fail("I couldn't understand that request — could you tell me again?"));

                var trip = _tripPlanRepo.GetByIdWithDetails(parsed.TripPlanId);
                if (trip == null || trip.TouristId != tourist.Id)
                    return Task.FromResult(Fail("I can't modify that trip plan."));

                var currentIds = trip.TripDestinations.Select(td => td.DestinationId).ToHashSet();
                var newIds = parsed.DestinationIds.ToHashSet();
                if (currentIds.Count != newIds.Count || !currentIds.SetEquals(newIds) ||
                    parsed.DestinationIds.Count != parsed.DestinationIds.Distinct().Count())
                {
                    return Task.FromResult(Fail("The destination list doesn't match the current stops in this trip. " +
                                                 "Please confirm the full new order by listing every destination exactly once."));
                }

                for (var i = 0; i < parsed.DestinationIds.Count; i++)
                {
                    var stop = trip.TripDestinations.First(td => td.DestinationId == parsed.DestinationIds[i]);
                    stop.Visit_Order = i + 1;
                    _tripPlanRepo.UpdateStop(stop);
                }
                _tripPlanRepo.Save();

                var names = parsed.DestinationIds.Select(id => _destinationRepo.GetById(id)?.Name ?? "unknown");
                var reply = $"Done! I've reordered **{trip.Title}** to: {string.Join(" → ", names)}.";
                return Task.FromResult(Ok(reply, new AiTripActionData { TripPlanId = trip.Id, TripPlanTitle = trip.Title }));
            }
        };

        // ============================================================
        // get_destination_photos
        // ============================================================
        private AiToolDefinition GetDestinationPhotos() => new()
        {
            Name = "get_destination_photos",
            Description = "Get the photo URLs for a specific destination by its ID. Use when the user asks to see photos of a place.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new { destination_id = new { type = "INTEGER", description = "The destination's ID." } },
                required = new[] { "destination_id" }
            },
            Roles = TouristRole,
            ExecuteAsync = (args, context, ct) =>
            {
                var parsed = AiToolsCommon.ParseArgs<GetPhotosArgs>(args);
                if (parsed == null)
                    return Task.FromResult(Fail("I couldn't understand that request."));

                var dest = _destinationRepo.GetById(parsed.DestinationId);
                if (dest == null || dest.Status != "Active")
                    return Task.FromResult(Fail("I couldn't find that destination in our catalog."));

                if (!dest.PhotoUrlList.Any())
                {
                    return Task.FromResult(Ok(
                        $"I don't have any photos for **{dest.Name}** right now, but here's what I can tell you: it's a {dest.Category ?? "popular"} destination in {dest.City}."));
                }

                return Task.FromResult(Ok($"Here are some photos of **{dest.Name}** in {dest.City}:",
                    new AiPhotoData { PhotoUrls = dest.PhotoUrlList }));
            }
        };

        // ============================================================
        // get_my_profile
        // ============================================================
        private AiToolDefinition GetMyProfile() => new()
        {
            Name = "get_my_profile",
            Description = "Show the signed-in tourist's profile: name, email, nationality, points, level, badges and preferences.",
            Parameters = new { type = "OBJECT", properties = new { } },
            Roles = TouristRole,
            ExecuteAsync = async (args, context, ct) =>
            {
                var tourist = context.Identity.Tourist;
                var user = context.Identity.User;
                if (tourist == null || user == null)
                    return Fail("You'll need to sign in first.");

                var progress = await _gamificationService.GetOrInitializeProgressAsync(tourist.Id);
                var (level, levelName, _) = LevelDefinitions.GetLevel(progress.CurrentXP);
                var badges = await _gamificationService.GetBadgesForTouristAsync(tourist.Id);

                var profile = new
                {
                    name = $"{user.FirstName} {user.LastName}".Trim(),
                    email = user.Email,
                    phone = user.PhoneNumber,
                    nationality = user.Nationality,
                    points = tourist.point_Balance,
                    level = levelName,
                    current_level = level,
                    badges = badges.Count,
                    preferred_language = tourist.PreferredLanguage,
                    travel_interests = tourist.TravelInterests
                };

                var message =
                    $"Here's your profile:\n" +
                    $"- Name: {profile.name}\n" +
                    $"- Email: {profile.email}\n" +
                    (string.IsNullOrWhiteSpace(profile.nationality) ? "" : $"- Nationality: {profile.nationality}\n") +
                    (string.IsNullOrWhiteSpace(profile.phone) ? "" : $"- Phone: {profile.phone}\n") +
                    $"- Points: {profile.points} (Level {profile.current_level} — {profile.level})\n" +
                    $"- Badges: {profile.badges}\n" +
                    (string.IsNullOrWhiteSpace(profile.travel_interests) ? "" : $"- Travel interests: {profile.travel_interests}\n") +
                    (string.IsNullOrWhiteSpace(profile.preferred_language) ? "" : $"- Preferred language: {profile.preferred_language}\n");

                return Ok(message.TrimEnd(), new { profile });
            }
        };

        // ============================================================
        // update_my_profile
        // ============================================================
        private AiToolDefinition UpdateMyProfile() => new()
        {
            Name = "update_my_profile",
            Description = "Update the signed-in tourist's OWN profile: first/last name, phone, nationality, preferred language, travel interests, " +
                          "and notification preferences. Only include fields the user wants to change. Email and profile picture cannot be changed through chat. " +
                          "The system will ask the user to confirm before saving.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new
                {
                    first_name = new { type = "STRING", description = "Optional new first name." },
                    last_name = new { type = "STRING", description = "Optional new last name." },
                    phone = new { type = "STRING", description = "Optional new phone number." },
                    nationality = new { type = "STRING", description = "Optional new nationality." },
                    preferred_language = new { type = "STRING", description = "Optional preferred language (e.g. en, ar)." },
                    travel_interests = new { type = "STRING", description = "Optional travel interests (e.g. \"temples, beaches\")." },
                    notify_by_email = new { type = "BOOLEAN", description = "Optional email notifications on/off." },
                    notify_in_app = new { type = "BOOLEAN", description = "Optional in-app notifications on/off." }
                }
            },
            Roles = TouristRole,
            RequiresConfirmation = true,
            ExecuteAsync = async (args, context, ct) =>
            {
                var tourist = context.Identity.Tourist;
                var user = context.Identity.User;
                if (tourist == null || user == null)
                    return Fail("You'll need to sign in first.");

                var parsed = AiToolsCommon.ParseArgs<UpdateProfileArgs>(args);
                if (parsed == null)
                    return Fail("I couldn't understand the profile changes.");

                var changed = new List<string>();
                if (!string.IsNullOrWhiteSpace(parsed.FirstName)) changed.Add($"first name → {parsed.FirstName.Trim()}");
                if (!string.IsNullOrWhiteSpace(parsed.LastName)) changed.Add($"last name → {parsed.LastName.Trim()}");
                if (parsed.Phone != null) changed.Add($"phone → {parsed.Phone.Trim()}");
                if (parsed.Nationality != null) changed.Add($"nationality → {parsed.Nationality.Trim()}");
                if (parsed.PreferredLanguage != null) changed.Add($"preferred language → {parsed.PreferredLanguage.Trim()}");
                if (parsed.TravelInterests != null) changed.Add($"travel interests → {parsed.TravelInterests.Trim()}");
                if (parsed.NotifyByEmail.HasValue) changed.Add($"email notifications → {(parsed.NotifyByEmail.Value ? "on" : "off")}");
                if (parsed.NotifyInApp.HasValue) changed.Add($"in-app notifications → {(parsed.NotifyInApp.Value ? "on" : "off")}");

                if (!changed.Any())
                    return Fail("I couldn't see any changes to apply. What would you like to update?");

                if (context.IsPreview)
                {
                    return Ok($"I'm ready to update your profile:\n- {string.Join("\n- ", changed)}");
                }

                user.FirstName = string.IsNullOrWhiteSpace(parsed.FirstName) ? user.FirstName : parsed.FirstName.Trim();
                user.LastName = string.IsNullOrWhiteSpace(parsed.LastName) ? user.LastName : parsed.LastName.Trim();
                if (parsed.Phone != null) user.PhoneNumber = parsed.Phone.Trim();
                if (parsed.Nationality != null) user.Nationality = parsed.Nationality.Trim();
                await _userManager.UpdateAsync(user);

                if (parsed.PreferredLanguage != null) tourist.PreferredLanguage = parsed.PreferredLanguage.Trim();
                if (parsed.TravelInterests != null) tourist.TravelInterests = parsed.TravelInterests.Trim();
                if (parsed.NotifyByEmail.HasValue) tourist.NotifyByEmail = parsed.NotifyByEmail.Value;
                if (parsed.NotifyInApp.HasValue) tourist.NotifyInApp = parsed.NotifyInApp.Value;
                _touristRepo.Update(tourist);
                _touristRepo.Save();

                return Ok("Done! Your profile has been updated successfully.");
            }
        };

        // ============================================================
        // get_recommended_destinations
        // ============================================================
        private AiToolDefinition GetRecommendedDestinations() => new()
        {
            Name = "get_recommended_destinations",
            Description = "Get destination recommendations for the tourist's next trip, ranked by community rating.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new { limit = new { type = "INTEGER", description = "How many recommendations (default 5)." } }
            },
            Roles = TouristRole,
            ExecuteAsync = (args, context, ct) =>
            {
                var parsed = AiToolsCommon.ParseArgs<GetRecommendationsArgs>(args);
                var limit = Math.Clamp(parsed?.Limit ?? 5, 1, 10);
                var top = _destinationRepo.GetAll()
                    .Where(d => d.Status == "Active")
                    .OrderByDescending(d => d.Rating ?? 0)
                    .ThenBy(d => d.Name)
                    .Take(limit)
                    .ToList();

                var items = top.Select(d => new
                {
                    id = d.Id,
                    name = d.Name,
                    city = d.City,
                    category = d.Category,
                    rating = d.Rating,
                    ticket_price_egp = d.TicketPrice
                }).ToList();

                var message = top.Count == 0
                    ? "I don't have any recommendations right now."
                    : $"Here are some great options for your next trip:\n" +
                      string.Join("\n", top.Select(AiToolsCommon.FormatDestination));

                return Task.FromResult(Ok(message, new { destinations = items }));
            }
        };

        // ============================================================
        // Helpers
        // ============================================================
        private HashSet<int> ActiveDestinationIds() =>
            _destinationRepo.GetAll().Where(d => d.Status == "Active").Select(d => d.Id).ToHashSet();

        private static AiToolResult Ok(string message, object? data = null) =>
            new() { Success = true, Message = message, Data = data };

        private static AiToolResult Fail(string message) =>
            new() { Success = false, Message = message };
    }
}
