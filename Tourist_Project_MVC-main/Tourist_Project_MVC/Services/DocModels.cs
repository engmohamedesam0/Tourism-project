using System.Collections.Generic;

namespace Tourist_Project_MVC.Services
{
    public class DocSection
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<DocArticle> Articles { get; set; } = new();
    }

    public class DocArticle
    {
        public string Section { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public int Order { get; set; }
        public string HtmlContent { get; set; } = string.Empty;
        public List<DocHeading> Headings { get; set; } = new();
        public string FilePath { get; set; } = string.Empty;
    }

    public class DocHeading
    {
        public string Text { get; set; } = string.Empty;
        public int Level { get; set; }
        public string Id { get; set; } = string.Empty;
    }

    public class DocSearchResult
    {
        public string Section { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Snippet { get; set; } = string.Empty;
    }
}
