using System.Collections.Concurrent;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tourist_Project_MVC.Services
{
    public class DocsService : IDocContentProvider
    {
        private readonly IHostEnvironment _env;
        private readonly ILogger<DocsService> _logger;
        private readonly string _docsRoot;
        private readonly MarkdownPipeline _pipeline;

        private static readonly IReadOnlyDictionary<string, string> SectionTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tourists"] = "For Tourists",
            ["sponsors"] = "For Sponsors",
            ["admins"] = "For Admins"
        };

        private volatile bool _loaded;
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private List<DocSection> _sections = new();
        private Dictionary<string, DocArticle> _articlesByKey = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, DocArticle> _articlesBySlug = new(StringComparer.OrdinalIgnoreCase);

        public DocsService(IHostEnvironment env, ILogger<DocsService> logger)
        {
            _env = env;
            _logger = logger;
            _docsRoot = Path.Combine(env.ContentRootPath, "Docs");
            _pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();
        }

        public async Task<IReadOnlyList<DocSection>> GetSectionsAsync(CancellationToken cancellationToken = default)
        {
            await EnsureLoadedAsync(cancellationToken);
            return _sections;
        }

        public async Task<DocArticle?> GetArticleAsync(string section, string slug, CancellationToken cancellationToken = default)
        {
            await EnsureLoadedAsync(cancellationToken);
            var key = $"{section}/{slug}";
            if (_articlesByKey.TryGetValue(key, out var article))
                return article;

            if (_articlesBySlug.TryGetValue(slug, out article))
                return article;

            return null;
        }

        public async Task<IReadOnlyList<DocSearchResult>> SearchAsync(string query, string? section = null, CancellationToken cancellationToken = default)
        {
            await EnsureLoadedAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(query))
                return Array.Empty<DocSearchResult>();

            var q = query.Trim().ToLowerInvariant();
            var results = new List<DocSearchResult>();
            var queryTerms = q.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var candidateArticles = _articlesByKey.Values.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(section))
            {
                candidateArticles = candidateArticles.Where(a => a.Section.Equals(section, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var article in candidateArticles)
            {
                var haystack = ($"{article.Title} {article.Description} {StripHtml(article.HtmlContent)}").ToLowerInvariant();
                bool matchesAll = queryTerms.All(term => haystack.Contains(term));
                if (!matchesAll)
                    continue;

                var snippet = ExtractSnippet(StripHtml(article.HtmlContent), queryTerms.FirstOrDefault() ?? q, 160);
                results.Add(new DocSearchResult
                {
                    Section = article.Section,
                    Slug = article.Slug,
                    Title = article.Title,
                    Description = article.Description,
                    Snippet = snippet
                });
            }

            return results;
        }

        public async Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            await _loadLock.WaitAsync(cancellationToken);
            try
            {
                _loaded = false;
                _sections = new List<DocSection>();
                _articlesByKey = new Dictionary<string, DocArticle>(StringComparer.OrdinalIgnoreCase);
                _articlesBySlug = new Dictionary<string, DocArticle>(StringComparer.OrdinalIgnoreCase);
                await LoadAsync(cancellationToken);
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
        {
            if (_loaded)
                return;

            await _loadLock.WaitAsync(cancellationToken);
            try
            {
                if (_loaded)
                    return;
                await LoadAsync(cancellationToken);
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            var sections = new Dictionary<string, (string Title, List<DocArticle> Articles)>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(_docsRoot))
            {
                _logger.LogWarning("Docs folder not found at {Path}", _docsRoot);
                _sections = new List<DocSection>();
                _loaded = true;
                return;
            }

            var mdFiles = Directory.GetFiles(_docsRoot, "*.md", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var file in mdFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relative = Path.GetRelativePath(_docsRoot, file);
                var sectionName = Path.GetDirectoryName(relative.Replace('\\', '/')) ?? string.Empty;
                var fileName = Path.GetFileNameWithoutExtension(file);

                var slug = Slugify(Regex.Replace(fileName, @"^\d+-", ""));
                var article = await ParseFileAsync(file, sectionName, slug, cancellationToken);
                if (article == null)
                    continue;

                if (!sections.TryGetValue(sectionName, out var sectionData))
                {
                    sectionData = (sectionName, new List<DocArticle>());
                    sections[sectionName] = sectionData;
                }

                article.FilePath = file;
                sectionData.Articles.Add(article);
                var key = $"{sectionName}/{slug}";
                _articlesByKey[key] = article;
                if (!_articlesBySlug.ContainsKey(slug))
                    _articlesBySlug[slug] = article;
            }

            _sections = sections.Select(kvp =>
            {
                var (id, data) = kvp;
                data.Articles.Sort((a, b) => a.Order.CompareTo(b.Order));
                return new DocSection
                {
                    Id = id,
                    Title = SectionTitles.TryGetValue(id, out var title) ? title : Titleize(id),
                    Articles = data.Articles
                };
            }).OrderBy(s => s.Id == "tourists" ? 0 : (s.Id == "sponsors" ? 1 : 2))
              .ThenBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
              .ToList();

            _loaded = true;
            _logger.LogInformation("Docs loaded: {Count} sections, {Articles} articles", _sections.Count, _articlesByKey.Count);
        }

        private async Task<DocArticle?> ParseFileAsync(string filePath, string section, string slug, CancellationToken cancellationToken)
        {
            var text = await File.ReadAllTextAsync(filePath, cancellationToken);
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');

            var frontMatterEnd = text.IndexOf("\n---", StringComparison.Ordinal);
            if (frontMatterEnd == -1)
            {
                _logger.LogWarning("Doc file missing front-matter closing delimiter: {Path}", filePath);
                return null;
            }

            var frontMatter = text.Substring(0, frontMatterEnd).Replace("---", "").Trim();
            var body = text.Substring(frontMatterEnd + 4).Trim();

            var title = string.Empty;
            var description = string.Empty;
            var category = string.Empty;
            var tags = new List<string>();
            var order = 999;

            foreach (var line in frontMatter.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                var idx = trimmed.IndexOf(':', StringComparison.Ordinal);
                if (idx <= 0)
                    continue;

                var key = trimmed.Substring(0, idx).Trim().ToLowerInvariant();
                var value = trimmed.Substring(idx + 1).Trim();
                if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
                    value = value[1..^1];
                else if (value.StartsWith("'") && value.EndsWith("'") && value.Length >= 2)
                    value = value[1..^1];

                switch (key)
                {
                    case "title":
                        title = value;
                        break;
                    case "description":
                        description = value;
                        break;
                    case "category":
                        category = value;
                        break;
                    case "tags":
                        var rawTags = value.Trim('[', ']');
                        tags = rawTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                       .Select(t => t.Trim('\'', '"'))
                                       .Where(t => !string.IsNullOrWhiteSpace(t))
                                       .ToList();
                        break;
                    case "order":
                        int.TryParse(value, out order);
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                _logger.LogWarning("Doc file missing title in front-matter: {Path}", filePath);
                return null;
            }

            if (string.IsNullOrWhiteSpace(category))
            {
                category = SectionTitles.TryGetValue(section, out var t) ? t : Titleize(section);
            }

            var html = Markdig.Markdown.ToHtml(body, _pipeline);
            html = ApplyAutolinks(html, section);
            var headings = ExtractHeadings(html);

            return new DocArticle
            {
                Section = section,
                Slug = slug,
                Title = title,
                Description = description,
                Category = category,
                Tags = tags,
                Order = order,
                HtmlContent = html,
                Headings = headings
            };
        }

        private static string ApplyAutolinks(string html, string section)
        {
            var isSponsor = section.Equals("sponsors", StringComparison.OrdinalIgnoreCase);
            var isAdmin = section.Equals("admins", StringComparison.OrdinalIgnoreCase);

            var linkMap = isAdmin
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Admin Dashboard"] = "/AdminDashboard",
                    ["Destinations"]    = "/Destination/Index",
                    ["Missions"]        = "/Mission/Index",
                    ["Rewards"]         = "/Reward/Index",
                    ["Sponsors"]        = "/Sponsor/Index",
                    ["Approvals"]       = "/SponsorApproval/Index",
                    ["Support Inbox"]   = "/AdminSupport/Index",
                    ["Utilities"]       = "/Utility/Index",
                    ["Accounts"]        = "/Role/ManageAccounts",
                    ["Tourists"]        = "/Tourist/Index",
                    ["Trip Plans"]      = "/TripPlan/Index"
                }
                : isSponsor
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Dashboard"] = "/SponsorPortal/Dashboard",
                    ["Rewards"]   = "/SponsorReward/Index",
                    ["Branches"]  = "/SponsorBranch/Index"
                }
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Trip Planner"] = "/TripPlan/Index",
                    ["Explore"]      = "/Explore/Index",
                    ["Rewards"]      = "/TouristReward/Index",
                    ["Profile"]      = "/TouristProfile/Index",
                    ["Support"]      = "/TouristSupport/Index"
                };

            // Process HTML tokens safely so we don't modify inside <a>, <code>, <pre>, or HTML tags
            var tokenPattern = @"(<a\b[^>]*>.*?</a>|<code\b[^>]*>.*?</code>|<pre\b[^>]*>.*?</pre>|<h[1-6]\b[^>]*>.*?</h[1-6]>|<[^>]+>)";
            var parts = Regex.Split(html, tokenPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            var usedTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sb = new StringBuilder();

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                if (part.StartsWith("<"))
                {
                    sb.Append(part);
                    continue;
                }

                var text = part;
                foreach (var kvp in linkMap)
                {
                    var term = kvp.Key;
                    var url = kvp.Value;

                    if (usedTerms.Contains(term))
                        continue;

                    var regex = new Regex($@"\b({Regex.Escape(term)})\b", RegexOptions.IgnoreCase);
                    if (regex.IsMatch(text))
                    {
                        text = regex.Replace(text, m =>
                        {
                            usedTerms.Add(term);
                            return $"<a class=\"docs-inline-link\" href=\"{url}\">{m.Value} <svg class=\"inline-arrow\" viewBox=\"0 0 16 16\" width=\"12\" height=\"12\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\"><path d=\"M4 12L12 4M12 4H6M12 4V10\"/></svg></a>";
                        }, 1);
                    }
                }

                sb.Append(text);
            }

            return sb.ToString();
        }

        private static List<DocHeading> ExtractHeadings(string html)
        {
            var headings = new List<DocHeading>();
            var matches = Regex.Matches(html, "<h([1-6])[^>]*>(.*?)</h\\1>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            foreach (Match m in matches)
            {
                var level = int.Parse(m.Groups[1].Value);
                var text = Regex.Replace(m.Groups[2].Value, "<[^>]+>", "").Trim();
                var id = Slugify(text);
                headings.Add(new DocHeading { Level = level, Text = text, Id = id });
            }
            return headings;
        }

        private static string Slugify(string input)
        {
            var sb = new StringBuilder();
            foreach (var ch in input.ToLowerInvariant())
            {
                sb.Append(ch switch
                {
                    >= 'a' and <= 'z' => ch,
                    >= '0' and <= '9' => ch,
                    ' ' or '-' or '_' => '-',
                    _ => '\0'
                });
            }
            var result = Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');
            return string.IsNullOrEmpty(result) ? "doc" : result;
        }

        private static string Titleize(string input)
        {
            return string.IsNullOrWhiteSpace(input) ? string.Empty : string.Concat(input.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1)));
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;
            return Regex.Replace(html, "<[^>]+>", " ").Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
        }

        private static string ExtractSnippet(string text, string term, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term))
                return text.Length <= maxLength ? text : text[..maxLength] + "…";

            var idx = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return text.Length <= maxLength ? text : text[..maxLength] + "…";

            var start = Math.Max(0, idx - maxLength / 2);
            var end = Math.Min(text.Length, idx + term.Length + maxLength / 2);
            var snippet = (start > 0 ? "…" : "") + text[start..end] + (end < text.Length ? "…" : "");
            return snippet;
        }
    }
}
