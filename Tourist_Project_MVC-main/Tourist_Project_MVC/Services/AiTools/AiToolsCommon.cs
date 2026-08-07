using System.Globalization;
using System.Text.Json;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Services.AiAgent;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Services.AiTools
{
    /// <summary>Shared helpers used by every role tool set.</summary>
    public static class AiToolsCommon
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new() { PropertyNameCaseInsensitive = true };

        public static T? ParseArgs<T>(JsonElement args)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(args.GetRawText(), JsonOptions);
            }
            catch (JsonException)
            {
                return default;
            }
        }

        /// <summary>Parses an ISO/date-string into a date. Falls back to the supplied fallback on failure.</summary>
        public static DateTime ParseDateOrDefault(string? value, DateTime fallback)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed.Date;
            }
            return fallback.Date;
        }

        /// <summary>
        /// Well-known coordinates for major Egyptian cities, used when the user
        /// names a place instead of raw coordinates (e.g. "in Giza").
        /// </summary>
        private static readonly Dictionary<string, (double Lat, double Lon)> EgyptCities = new(StringComparer.OrdinalIgnoreCase)
        {
            ["cairo"] = (30.0444, 31.2357),
            ["giza"] = (30.0131, 31.2089),
            ["alexandria"] = (31.2001, 29.9187),
            ["luxor"] = (25.6872, 32.6396),
            ["aswan"] = (24.0889, 32.8998),
            ["hurghada"] = (27.2579, 33.8116),
            ["sharm el sheikh"] = (27.9158, 34.3300),
            ["sharm"] = (27.9158, 34.3300),
            ["marsa alam"] = (25.0681, 34.8934),
            ["dahab"] = (28.5096, 34.5165),
            ["siwa"] = (29.2032, 25.5197),
            ["port said"] = (31.2653, 32.3019),
            ["ismailia"] = (30.5965, 32.2715),
            ["suez"] = (29.9668, 32.5498),
            ["minya"] = (28.1099, 30.7503),
            ["asyut"] = (27.1783, 31.1859),
            ["edfu"] = (24.9780, 32.8732),
            ["kom ombo"] = (24.4568, 32.9283),
            ["abu simbel"] = (22.3372, 31.6258),
            ["tanta"] = (30.7885, 31.0019),
            ["mansoura"] = (31.0409, 31.3785),
            ["zagazig"] = (30.5877, 31.5020),
            ["fayoum"] = (29.3084, 30.8428),
            ["beni suef"] = (29.0661, 31.0994),
            ["qena"] = (26.1640, 32.7260),
            ["sohag"] = (26.5566, 31.6948),
            ["damietta"] = (31.4170, 31.8144)
        };

        /// <summary>
        /// Resolves a location for branch/destination creation: explicit lat/long
        /// wins; otherwise the named city is looked up; otherwise fails so the
        /// model asks the user for a recognizable city or coordinates.
        /// </summary>
        public static bool TryResolveLocation(double? latitude, double? longitude, string? city, out double lat, out double lon)
        {
            if (latitude.HasValue && longitude.HasValue)
            {
                lat = latitude.Value;
                lon = longitude.Value;
                return lat is >= -90 and <= 90 && lon is >= -180 and <= 180;
            }

            if (!string.IsNullOrWhiteSpace(city))
            {
                var normalized = city.Trim().ToLowerInvariant();
                if (EgyptCities.TryGetValue(normalized, out var known))
                {
                    lat = known.Lat;
                    lon = known.Lon;
                    return true;
                }
                // "new cairo", "6th of october" style partial matches
                foreach (var kv in EgyptCities)
                {
                    if (normalized.Contains(kv.Key) || kv.Key.Contains(normalized))
                    {
                        lat = kv.Value.Lat;
                        lon = kv.Value.Lon;
                        return true;
                    }
                }
            }

            lat = 0;
            lon = 0;
            return false;
        }

        public static string FormatDestination(Destination d)
        {
            var price = d.TicketPrice.HasValue ? d.TicketPrice.Value.ToString("0.##") + " EGP" : "free";
            var rating = d.Rating.HasValue ? d.Rating.Value.ToString("0.0") : "n/a";
            return $"{d.Name} (id={d.Id}) — {d.City}, {d.Category ?? "General"}, {price}, rating {rating}";
        }

        public static string ShortDate(DateTime d) => d.ToString("MMM d, yyyy");

        /// <summary>Human-friendly duration between two dates.</summary>
        public static string DurationLabel(DateTime start, DateTime end)
        {
            var days = (int)(end.Date - start.Date).TotalDays + 1;
            return days <= 1 ? "1 day" : $"{days} days";
        }
    }
}
