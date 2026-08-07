using Tourist_Project_MVC.Services.AiAgent;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Services
{
    /// <summary>
    /// Facade kept for compatibility with the existing chat endpoint: the actual
    /// role-aware agent logic now lives in AiAgentOrchestrator.
    /// </summary>
    public class AiChatService : IAiChatService
    {
        private readonly IAiAgentOrchestrator _orchestrator;

        public AiChatService(IAiAgentOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        public Task<AiChatResponseVM> GetReplyAsync(AiChatRequestVM request, AiIdentityContext identity, CancellationToken ct = default)
            => _orchestrator.GetReplyAsync(request, identity, ct);
    }
}
