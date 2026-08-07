using System.Text.Json;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services.AiAgent;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Services.AiTools
{
    /// <summary>
    /// Public, read-only tools available to everyone — including guests. None of
    /// these can create, update, or delete anything; the registry enforces that
    /// on top of the per-tool code.
    /// </summary>
    public class GuestAiTools
    {
        private static readonly string[] AllRoles =
        {
            AiIdentityContext.RoleGuest, AiIdentityContext.RoleTourist,
            AiIdentityContext.RoleSponsor, AiIdentityContext.RoleAdmin
        };

        private readonly IDestinationRepository _destinationRepo;
        private readonly IRewardRepository _rewardRepo;

        public GuestAiTools(IDestinationRepository destinationRepo, IRewardRepository rewardRepo)
        {
            _destinationRepo = destinationRepo;
            _rewardRepo = rewardRepo;
        }

        public List<AiToolDefinition> Build() => new()
        {
            SearchDestinations(),
            GetDestinationDetails(),
            GetPublicRewards(),
            GetSiteOverview(),
            GetRecommendations()
        };

        // ---------------------------------------------------------------
        // search_destinations
        // ---------------------------------------------------------------
        private AiToolDefinition SearchDestinations() => new()
        {
            Name = "search_destinations",
            Description = "Search the public destination catalog. Returns real destinations matching a free-text query, city, and/or category. " +
                          "Use this whenever the user asks about places, or when a destination name needs to be matched to its real ID.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new
                {
                    query = new { type = "STRING", description = "Optional free-text search (name, description, tags)." },
                    city = new { type = "STRING", description = "Optional city/governorate filter (e.g. Luxor, Cairo)." },
                    category = new { type = "STRING", description = "Optional category filter (e.g. Temple, Museum, Beach)." },
                    limit = new { type = "INTEGER", description = "Max results to return (default 10)." }
                }
            },
            Roles = AllRoles,
            ExecuteAsync = async (args, context, ct) =>
            {
                var parsed = AiToolsCommon.ParseArgs<SearchDestinationsArgs>(args);
                if (parsed == null)
                    return Fail("I couldn't understand that search. Could you rephrase it?");

                var query = parsed.Query?.Trim();
                var results = _destinationRepo.GetAll()
                    .Where(d => d.Status == "Active")
                    .AsEnumerable()
                    .Where(d =>
                        (string.IsNullOrWhiteSpace(query)
                         || d.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || (d.City?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                         || (d.Category?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                         || (d.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                         || (d.Tags?.Contains(query, StringComparison.OrdinalIgnoreCase) == true))
                        && (string.IsNullOrWhiteSpace(parsed.City) || string.Equals(d.City, parsed.City.Trim(), StringComparison.OrdinalIgnoreCase))
                        && (string.IsNullOrWhiteSpace(parsed.Category) || string.Equals(d.Category, parsed.Category.Trim(), StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(d => d.Name)
                    .Take(Math.Clamp(parsed.Limit ?? 10, 1, 30))
                    .ToList();

                var items = results.Select(d => new
                {
                    id = d.Id,
                    name = d.Name,
                    city = d.City,
                    category = d.Category,
                    ticket_price_egp = d.TicketPrice,
                    rating = d.Rating,
                    description = d.Description
                }).ToList();

                var message = results.Count == 0
                    ? "I couldn't find any destinations matching that."
                    : $"I found {results.Count} destination{(results.Count == 1 ? "" : "s")}:\n" +
                      string.Join("\n", results.Select(AiToolsCommon.FormatDestination));

                return Ok(message, new { destinations = items });
            }
        };

        // ---------------------------------------------------------------
        // get_destination_details
        // ---------------------------------------------------------------
        private AiToolDefinition GetDestinationDetails() => new()
        {
            Name = "get_destination_details",
            Description = "Get full details of a single destination by its ID (from search_destinations). " +
                          "Use when the user asks about a specific place: hours, ticket prices, description, photos.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new
                {
                    destination_id = new { type = "INTEGER", description = "The destination ID from the catalog." }
                },
                required = new[] { "destination_id" }
            },
            Roles = AllRoles,
            ExecuteAsync = (args, context, ct) =>
            {
                var parsed = AiToolsCommon.ParseArgs<GetDestinationDetailsArgs>(args);
                if (parsed == null || parsed.DestinationId <= 0)
                    return Task.FromResult(Fail("I need a valid destination to look up."));

                var d = _destinationRepo.GetById(parsed.DestinationId);
                if (d == null || d.Status != "Active")
                    return Task.FromResult(Fail("I couldn't find that destination in our catalog."));

                var details = new
                {
                    id = d.Id,
                    name = d.Name,
                    arabic_name = d.ArabicName,
                    city = d.City,
                    category = d.Category,
                    description = d.Description,
                    ticket_required = d.TicketRequired,
                    ticket_price_egp = d.TicketPrice,
                    egyptian_price = d.EgyptianPrice,
                    student_egyptian_price = d.StudentEgyptianPrice,
                    foreign_price = d.ForeignPrice,
                    student_foreign_price = d.StudentForeignPrice,
                    open_at = d.OpenAt,
                    close_at = d.CloseAt,
                    days = d.Days,
                    booking = d.Booking,
                    rating = d.Rating,
                    tags = d.Tags,
                    photo_urls = d.PhotoUrlList
                };

                var message = $"{d.Name} — {d.City} ({d.Category ?? "General"})" +
                              (string.IsNullOrWhiteSpace(d.Description) ? "" : $"\n{d.Description}") +
                              $"\nTicket: {(d.TicketRequired ?? "Yes")}" +
                              (d.TicketPrice.HasValue ? $" — {d.TicketPrice.Value:0.##} EGP" : "") +
                              (d.OpenAt.HasValue && d.CloseAt.HasValue ? $"\nHours: {d.OpenAt}:00 – {d.CloseAt}:00" : "") +
                              (string.IsNullOrWhiteSpace(d.Days) ? "" : $"\nOpen days: {d.Days}");

                return Task.FromResult(Ok(message, new { destination = details }));
            }
        };

        // ---------------------------------------------------------------
        // get_public_rewards
        // ---------------------------------------------------------------
        private AiToolDefinition GetPublicRewards() => new()
        {
            Name = "get_public_rewards",
            Description = "List the public rewards/offers currently available on the platform (active rewards only). " +
                          "Use when the user asks what rewards, offers, or points deals exist.",
            Parameters = new { type = "OBJECT", properties = new { } },
            Roles = AllRoles,
            ExecuteAsync = (args, context, ct) =>
            {
                var rewards = _rewardRepo.GetAll()
                    .Where(r => r.Status == "Active")
                    .OrderBy(r => r.PointsRequired)
                    .Take(20)
                    .ToList();

                var items = rewards.Select(r => new
                {
                    id = r.Id,
                    title = r.Title,
                    reward_type = r.RewardType,
                    description = r.Description,
                    points_required = r.PointsRequired,
                    quantity_available = r.QuantityAvailable,
                    expires = r.ExpirationDate.ToString("yyyy-MM-dd"),
                    sponsor = r.Sponsor?.Name
                }).ToList();

                var message = rewards.Count == 0
                    ? "There are no active rewards right now."
                    : $"Here are the current rewards:\n" +
                      string.Join("\n", rewards.Select(r => $"- {r.Title} ({r.RewardType}) — {r.PointsRequired} points" +
                                                            (r.Sponsor != null ? $" by {r.Sponsor.Name}" : "") +
                                                            $" | expires {r.ExpirationDate:MMM d, yyyy}"));

                return Task.FromResult(Ok(message, new { rewards = items }));
            }
        };

        // ---------------------------------------------------------------
        // get_site_overview
        // ---------------------------------------------------------------
        private AiToolDefinition GetSiteOverview() => new()
        {
            Name = "get_site_overview",
            Description = "Overview of what EGYXPLORE is and what visitors can do on the website. " +
                          "Use when the user asks \"what is EGYXPLORE\", \"what can I do here\", or how the platform works.",
            Parameters = new { type = "OBJECT", properties = new { } },
            Roles = AllRoles,
            ExecuteAsync = (args, context, ct) =>
            {
                var destinationCount = _destinationRepo.GetAll().Count(d => d.Status == "Active");
                var message =
                    $"EGYXPLORE is a tourism platform about Egypt. Here's what you can do:\n\n" +
                    $"- Explore {destinationCount} destinations (temples, museums, beaches, historical sites) with details, photos, ticket prices and ratings on the Explore page.\n" +
                    "- Use \"Near Me\" to find places close to you, and the interactive map.\n" +
                    "- Plan trips: build an itinerary with your chosen stops, dates and budget, then keep it in your profile.\n" +
                    "- Earn points through missions, badges and levels, and redeem them for rewards offered by sponsors at their branches.\n" +
                    "- Read and write reviews of destinations, trips, and sponsor experiences.\n" +
                    "- Sponsors manage their own branches and rewards; admins manage the platform's content and users.\n\n" +
                    "Ask me for destination recommendations, trip planning help, or anything about Egypt!";

                return Task.FromResult(Ok(message, new { destination_count = destinationCount }));
            }
        };

        // ---------------------------------------------------------------
        // get_recommendations
        // ---------------------------------------------------------------
        private AiToolDefinition GetRecommendations() => new()
        {
            Name = "get_recommendations",
            Description = "Get recommended destinations based on the community rating. Use when the user asks for the best places, " +
                          "top destinations, or ideas for what to visit.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new { limit = new { type = "INTEGER", description = "How many recommendations (default 5)." } }
            },
            Roles = AllRoles,
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
                    : $"Here are some of the best-rated destinations:\n" +
                      string.Join("\n", top.Select(AiToolsCommon.FormatDestination));

                return Task.FromResult(Ok(message, new { destinations = items }));
            }
        };

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------
        private static AiToolResult Ok(string message, object? data = null) =>
            new() { Success = true, Message = message, Data = data };

        private static AiToolResult Fail(string message) =>
            new() { Success = false, Message = message };
    }
}
