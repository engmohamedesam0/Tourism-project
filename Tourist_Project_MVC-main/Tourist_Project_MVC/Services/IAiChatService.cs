using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Services
{
    public interface IAiChatService
    {
        // userEmail: the authenticated user's email resolved server-side from
        // the auth identity (never from frontend input). Used as the ownership
        // key for persisted chat sessions.
        Task<AiChatResponseVM> GetReplyAsync(AiChatRequestVM request, Tourist? tourist, string? userEmail, CancellationToken ct = default);
    }
}
