using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Services
{
    public interface IAiChatService
    {
        Task<AiChatResponseVM> GetReplyAsync(AiChatRequestVM request, Tourist? tourist, CancellationToken ct = default);
    }
}
