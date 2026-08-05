using Tourist_Project_MVC.Services.AiAgent;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Services
{
    public interface IAiChatService
    {
        // identity: server-resolved authentication state (user id, role, tourist
        // / sponsor records) — never supplied by the client or the AI model.
        Task<AiChatResponseVM> GetReplyAsync(AiChatRequestVM request, AiIdentityContext identity, CancellationToken ct = default);
    }
}
