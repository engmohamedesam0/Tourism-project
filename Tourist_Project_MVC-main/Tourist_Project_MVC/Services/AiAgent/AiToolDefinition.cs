using System.Text.Json;

namespace Tourist_Project_MVC.Services.AiAgent
{
    /// <summary>
    /// Everything a tool needs to execute: the server-resolved identity (never
    /// user-supplied), the raw HTTP context, and the cancellation token.
    /// </summary>
    public class AiToolContext
    {
        public required AiIdentityContext Identity { get; init; }
        public required HttpContext HttpContext { get; init; }
        public int? ChatSessionId { get; init; }

        /// <summary>
        /// True during the confirmation pre-validation pass: the tool must
        /// VALIDATE and produce its summary but must NOT write anything. The
        /// real write happens later, when the user confirms, with IsPreview=false.
        /// </summary>
        public bool IsPreview { get; init; }

        public CancellationToken CancellationToken { get; init; }
    }

    /// <summary>
    /// Result of executing one AI tool.
    /// </summary>
    public class AiToolResult
    {
        public bool Success { get; init; }

        /// <summary>Safe, human-readable message (used as fallback reply and inside confirmation summaries).</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// True when the tool PRE-VALIDATED the operation but did NOT write.
        /// The orchestrator then stops and asks the user to confirm via the
        /// pending-action token. The actual write only happens when the user
        /// confirms (fresh identity checks are performed again at that moment).
        /// </summary>
        public bool NeedsConfirmation { get; init; }

        public string? ConfirmationToken { get; init; }
        public string? ConfirmationSummary { get; init; }

        /// <summary>Structured data sent back to the model (function response).</summary>
        public object? Data { get; init; }

        /// <summary>Machine-readable error code (never surfaced verbatim to the user).</summary>
        public string? ErrorCode { get; init; }
    }

    /// <summary>
    /// One AI-callable tool. Registration is role-based: the registry only hands
    /// the model the tools whose Roles[] contains the current role, and the
    /// executor re-checks the role before every execution — the model can never
    /// widen its own permissions.
    /// </summary>
    public class AiToolDefinition
    {
        public required string Name { get; init; }
        public required string Description { get; init; }

        /// <summary>Gemini JSON-schema parameters object (same shape as the existing GeminiFunctionDeclaration.Parameters).</summary>
        public required object Parameters { get; init; }

        /// <summary>Roles allowed to call this tool: AiIdentityContext.RoleGuest / RoleTourist / RoleSponsor / RoleAdmin.</summary>
        public required string[] Roles { get; init; }

        /// <summary>
        /// True for state-changing operations (create/update/delete/price changes).
        /// The executor intercepts these and returns NeedsConfirmation instead of
        /// writing, until the user confirms the stored pending action.
        /// </summary>
        public bool RequiresConfirmation { get; init; }

        public Func<JsonElement, AiToolContext, CancellationToken, Task<AiToolResult>> ExecuteAsync { get; set; } =
            (_, _, _) => Task.FromResult(new AiToolResult { Success = false, Message = "Not implemented." });

        public bool AllowedFor(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

        public string DisplayName => Name;
    }
}
