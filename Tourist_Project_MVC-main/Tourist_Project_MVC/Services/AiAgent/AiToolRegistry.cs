using System.Text.Json;
using Tourist_Project_MVC.Services.AiTools;

namespace Tourist_Project_MVC.Services.AiAgent
{
    /// <summary>
    /// Central registry of every AI tool. Tools are grouped into per-role builder
    /// classes (Guest/Tourist/Sponsor/Admin) and merged here. Some tool names are
    /// intentionally role-specific (e.g. create_reward differs for Sponsor and
    /// Admin), so the registry dispatches by (name, role) — each role gets its
    /// own implementation even when the wire name is the same.
    ///
    /// The registry is the ONLY entry point for execution — it re-checks the
    /// current role and routes state-changing operations through the
    /// confirmation gate.
    /// </summary>
    public interface IAiToolRegistry
    {
        IReadOnlyList<AiToolDefinition> AllTools { get; }
        IReadOnlyList<AiToolDefinition> GetToolsForRole(string role);
        AiToolDefinition? FindForRole(string role, string name);

        /// <summary>
        /// Executes a tool for the given identity.
        /// - allowWrite=false (model-initiated): state-changing tools are
        ///   intercepted — the operation is pre-validated and stored as a pending
        ///   action (result.NeedsConfirmation=true), nothing is written.
        /// - allowWrite=true (user-confirmed): the stored operation executes with
        ///   a FRESH identity/role/ownership check at this exact moment.
        /// </summary>
        Task<AiToolResult> ExecuteAsync(string name, JsonElement args, AiToolContext context, bool allowWrite, CancellationToken ct);
    }

    public class AiToolRegistry : IAiToolRegistry
    {
        // name -> role -> tool (per-role dispatch for same-named tools)
        private readonly Dictionary<string, Dictionary<string, AiToolDefinition>> _tools;
        private readonly AiPendingActionStore _pendingStore;

        public AiToolRegistry(
            GuestAiTools guestTools,
            TouristAiTools touristTools,
            SponsorAiTools sponsorTools,
            AdminAiTools adminTools,
            AiPendingActionStore pendingStore)
        {
            _pendingStore = pendingStore;

            _tools = new Dictionary<string, Dictionary<string, AiToolDefinition>>(StringComparer.Ordinal);
            var all = guestTools.Build()
                .Concat(touristTools.Build())
                .Concat(sponsorTools.Build())
                .Concat(adminTools.Build());

            foreach (var tool in all)
            {
                foreach (var role in tool.Roles)
                {
                    if (!_tools.TryGetValue(tool.Name, out var byRole))
                    {
                        byRole = new Dictionary<string, AiToolDefinition>(StringComparer.Ordinal);
                        _tools[tool.Name] = byRole;
                    }
                    byRole[role] = tool;
                }
            }
        }

        public IReadOnlyList<AiToolDefinition> AllTools =>
            _tools.Values
                .SelectMany(byRole => byRole.Values)
                .GroupBy(t => t.Name)
                .Select(g => g.First())
                .ToList();

        public IReadOnlyList<AiToolDefinition> GetToolsForRole(string role)
        {
            var result = new List<AiToolDefinition>();
            foreach (var byRole in _tools.Values)
            {
                if (byRole.TryGetValue(role, out var tool))
                    result.Add(tool);
            }
            return result.OrderBy(t => t.Name).ToList();
        }

        public AiToolDefinition? FindForRole(string role, string name)
        {
            return _tools.TryGetValue(name, out var byRole) && byRole.TryGetValue(role, out var tool)
                ? tool
                : null;
        }

        public async Task<AiToolResult> ExecuteAsync(string name, JsonElement args, AiToolContext context, bool allowWrite, CancellationToken ct)
        {
            if (!_tools.TryGetValue(name, out var byRole) || !byRole.TryGetValue(context.Identity.Role, out var tool))
                return new AiToolResult { Success = false, Message = "I don't have that capability right now." };

            // Authorization is ALWAYS server-side: the model can only ever call
            // tools its role allows, and even a malicious function call cannot
            // bypass this check (it also re-runs at confirmation time).
            if (!tool.AllowedFor(context.Identity.Role))
                return new AiToolResult
                {
                    Success = false,
                    Message = "You don't have permission to do that with your current account.",
                    ErrorCode = "forbidden"
                };

            // Confirmation gate: model-initiated state changes are never executed
            // directly. Pre-validate by running the tool's own validation, then
            // park it as a pending action for the user to confirm.
            if (tool.RequiresConfirmation && !allowWrite)
            {
                // Only one pending action at a time per user keeps the
                // confirmation flow unambiguous.
                if (context.Identity.UserId != null &&
                    _pendingStore.PeekForUser(context.Identity.UserId) != null)
                {
                    return new AiToolResult
                    {
                        Success = false,
                        Message = "There's already an action waiting for your confirmation. " +
                                  "Please confirm or cancel it first before I start anything new.",
                        ErrorCode = "pending_action_exists"
                    };
                }

                var previewContext = new AiToolContext
                {
                    Identity = context.Identity,
                    HttpContext = context.HttpContext,
                    ChatSessionId = context.ChatSessionId,
                    IsPreview = true,
                    CancellationToken = context.CancellationToken
                };
                var preview = await RunToolAsync(tool, args, previewContext, ct);
                if (!preview.Success)
                    return preview; // validation failed -> tell the user why

                if (context.Identity.User == null)
                    return new AiToolResult
                    {
                        Success = false,
                        Message = "You'll need to sign in before I can do that.",
                        ErrorCode = "not_authenticated"
                    };

                var pending = new AiPendingAction
                {
                    Token = AiPendingActionStore.NewToken(),
                    UserId = context.Identity.UserId!,
                    Role = context.Identity.Role,
                    ToolName = tool.Name,
                    ArgsJson = args.GetRawText(),
                    Summary = preview.Message,
                    ChatSessionId = context.ChatSessionId
                };
                _pendingStore.Store(pending);

                return new AiToolResult
                {
                    Success = true,
                    NeedsConfirmation = true,
                    ConfirmationToken = pending.Token,
                    ConfirmationSummary = preview.Message,
                    Message = preview.Message
                };
            }

            return await RunToolAsync(tool, args, context, ct);
        }

        private static async Task<AiToolResult> RunToolAsync(AiToolDefinition tool, JsonElement args, AiToolContext context, CancellationToken ct)
        {
            try
            {
                return await tool.ExecuteAsync(args, context, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Never leak internals to the chat: log the real error, return a
                // generic human-readable failure.
                var logger = context.HttpContext.RequestServices
                    .GetService(typeof(ILogger<AiToolRegistry>)) as ILogger<AiToolRegistry>;
                logger?.LogError(ex, "AI tool {Tool} failed.", tool.Name);
                return new AiToolResult
                {
                    Success = false,
                    Message = "Something went wrong while handling that request. Please try again.",
                    ErrorCode = "internal"
                };
            }
        }
    }
}
