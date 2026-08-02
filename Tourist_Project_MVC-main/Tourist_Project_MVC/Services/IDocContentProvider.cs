using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tourist_Project_MVC.Services
{
    public interface IDocContentProvider
    {
        Task<IReadOnlyList<DocSection>> GetSectionsAsync(CancellationToken cancellationToken = default);
        Task<DocArticle?> GetArticleAsync(string section, string slug, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DocSearchResult>> SearchAsync(string query, string? section = null, CancellationToken cancellationToken = default);
        Task ReloadAsync(CancellationToken cancellationToken = default);
    }
}
