using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;

namespace Tourist_Project_MVC.Controllers
{
    // Tourist-facing discovery page. Read-only browsing of destinations.
    // Guests may browse read-only; Trip planning is gated behind sign-in elsewhere.
    public class ExploreController : Controller
    {
        private readonly IDestinationRepository _repo;

        // Related-word groups for Egyptian tourism search. When a user types
        // any word in a group, all other words in that group are also matched.
        private static readonly string[][] SynonymGroups = new[]
        {
            new[] { "pyramid", "pyramids", "giza", "khufu", "khafre", "menkaure", "djoser", "dahshur", "saqqara", "pharaonic" },
            new[] { "temple", "temples", "karnak", "luxor", "hatshepsut", "philae", "edfu", "abu simbel", "pharaonic" },
            new[] { "mosque", "mosques", "islamic", "masjid", "sultan", "citadel" },
            new[] { "church", "churches", "coptic", "cathedral", "monastery" },
            new[] { "museum", "museums", "gallery", "galleries", "exhibition" },
            new[] { "tomb", "tombs", "burial", "mummy", "mummies", "sarcophagus", "necropolis", "valley" },
            new[] { "palace", "palaces", "castle", "citadel", "fort", "fortress" },
            new[] { "park", "parks", "garden", "gardens", "nature", "natural", "reserve", "oasis" },
            new[] { "beach", "beaches", "sea", "coast", "coastal", "diving", "snorkeling", "reef", "coral" },
            new[] { "desert", "safari", "adventure", "camping", "dune", "dunes", "oasis", "siwa" },
            new[] { "bazaar", "market", "markets", "souq", "souk", "shopping", "khan", "khalili" },
            new[] { "nile", "river", "cruise", "felucca", "boat" },
            new[] { "ancient", "old", "historic", "historical", "heritage", "monument", "monuments", "ruins", "ruin" },
            new[] { "sphinx" },
            new[] { "pharaoh", "pharaonic", "pharaohs" },
            new[] { "hidden", "hidden gem", "hidden gems", "secret", "undiscovered" },
            new[] { "street", "streets", "road", "walk", "walking" },
            new[] { "coptic", "christian" },
            new[] { "islamic", "islam" },
            new[] { "statue", "statues", "sculpture", "obelisk" },
            new[] { "ramses", "ramesses" },
            new[] { "tutankhamun", "tut" },
            new[] { "roman", "greco-roman", "amphitheatre" },
            new[] { "library", "bibliotheca" },
        };

        // Lazily built lookup: word → all related words.
        private static Dictionary<string, HashSet<string>>? _synonymLookup;
        private static Dictionary<string, HashSet<string>> SynonymLookup
        {
            get
            {
                if (_synonymLookup != null) return _synonymLookup;
                var map = new Dictionary<string, HashSet<string>>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var group in SynonymGroups)
                {
                    foreach (var word in group)
                    {
                        if (!map.ContainsKey(word))
                            map[word] = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                        foreach (var syn in group)
                            map[word].Add(syn);
                    }
                }
                _synonymLookup = map;
                return _synonymLookup;
            }
        }

        /// <summary>
        /// Expands a search term into itself + all synonyms from our dictionary.
        /// </summary>
        private static IEnumerable<string> ExpandTerm(string term)
        {
            yield return term;
            if (SynonymLookup.TryGetValue(term, out var synonyms))
            {
                foreach (var syn in synonyms)
                    if (!string.Equals(syn, term, System.StringComparison.OrdinalIgnoreCase))
                        yield return syn;
            }
            // Also check partial matches: if term is a substring of a synonym key
            foreach (var kvp in SynonymLookup)
            {
                if (kvp.Key.Length > 3 && kvp.Key.Contains(term, System.StringComparison.OrdinalIgnoreCase) && !string.Equals(kvp.Key, term, System.StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var syn in kvp.Value)
                        yield return syn;
                }
            }
        }

        /// <summary>
        /// Checks if a destination matches a single search term (with synonym expansion).
        /// </summary>
        private static bool DestinationMatchesTerm(Destination d, string term)
        {
            var expanded = ExpandTerm(term).Distinct(System.StringComparer.OrdinalIgnoreCase);
            foreach (var t in expanded)
            {
                if ((d.Name != null && d.Name.Contains(t, System.StringComparison.OrdinalIgnoreCase)) ||
                    (d.ArabicName != null && d.ArabicName.Contains(t, System.StringComparison.OrdinalIgnoreCase)) ||
                    (d.City != null && d.City.Contains(t, System.StringComparison.OrdinalIgnoreCase)) ||
                    (d.Category != null && d.Category.Contains(t, System.StringComparison.OrdinalIgnoreCase)) ||
                    (d.Tags != null && d.Tags.Contains(t, System.StringComparison.OrdinalIgnoreCase)) ||
                    (d.Description != null && d.Description.Contains(t, System.StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            return false;
        }

        public ExploreController(IDestinationRepository repo)
        {
            _repo = repo;
        }

        public IActionResult Index(string? search)
        {
            var all = _repo.GetAll();

            ViewBag.Categories = all
                .Where(d => !string.IsNullOrWhiteSpace(d.Category))
                .Select(d => d.Category!)
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, System.StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var terms = search.Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
                foreach (var term in terms)
                {
                    var captured = term; // capture for closure
                    all = all.Where(d => DestinationMatchesTerm(d, captured));
                }
            }

            ViewBag.Search = search;
            return View(all);
        }
    }
}

