using System.Collections.Concurrent;

namespace Tourist_Project_MVC.Services.AiAgent
{
    /// <summary>
    /// A state-changing operation the model proposed but which must NOT run until
    /// the user confirms it. Stored server-side under an opaque, random token so
    /// the client can never re-submit or tamper with the arguments. Bound to the
    /// UserId that created it, so a token alone cannot be used by another user.
    /// </summary>
    public class AiPendingAction
    {
        public required string Token { get; init; }
        public required string UserId { get; init; }
        public required string Role { get; init; }
        public required string ToolName { get; init; }
        public required string ArgsJson { get; init; }
        public required string Summary { get; init; }
        public int? ChatSessionId { get; init; }
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Thread-safe in-memory store of pending confirmations. Entries expire after
    /// PendingActionTtlMinutes (default 10) and are purged lazily on access.
    /// NOTE: in-memory by design (ephemeral confirmation state, survives only the
    /// current process). A confirmation is re-validated against the CURRENT
    /// identity + authorization at execution time, so nothing security-relevant
    /// depends on this store's persistence.
    /// </summary>
    public class AiPendingActionStore
    {
        private readonly ConcurrentDictionary<string, AiPendingAction> _actions = new(StringComparer.Ordinal);
        private readonly TimeSpan _ttl;

        public TimeSpan Ttl => _ttl;

        public AiPendingActionStore(TimeSpan? ttl = null)
        {
            _ttl = ttl ?? TimeSpan.FromMinutes(10);
        }

        public string Store(AiPendingAction action)
        {
            _actions[action.Token] = action;
            return action.Token;
        }

        /// <summary>Returns the pending action for a token if it exists, is unexpired and belongs to userId — otherwise null. Does not remove it.</summary>
        public AiPendingAction? Peek(string token, string userId)
        {
            if (!_actions.TryGetValue(token, out var action)) return null;
            if (action.UserId != userId) return null;
            if (DateTime.UtcNow - action.CreatedAt > _ttl)
            {
                _actions.TryRemove(token, out _);
                return null;
            }
            return action;
        }

        /// <summary>Atomically removes and returns the pending action if valid for userId; null otherwise.</summary>
        public AiPendingAction? Consume(string token, string userId)
        {
            var action = Peek(token, userId);
            if (action == null) return null;
            _actions.TryRemove(token, out _);
            return action;
        }

        /// <summary>Returns the (unexpired) pending action for a user, if any. Only one pending action per user is allowed at a time.</summary>
        public AiPendingAction? PeekForUser(string userId)
        {
            foreach (var kv in _actions)
            {
                var action = kv.Value;
                if (action.UserId == userId && DateTime.UtcNow - action.CreatedAt <= _ttl)
                    return action;
            }
            return null;
        }

        public bool Remove(string token) => _actions.TryRemove(token, out _);

        /// <summary>Removes any pending action belonging to a session (e.g. session deleted).</summary>
        public void ClearForSession(int chatSessionId)
        {
            foreach (var kv in _actions)
            {
                if (kv.Value.ChatSessionId == chatSessionId)
                    _actions.TryRemove(kv.Key, out _);
            }
        }

        public static string NewToken() => Guid.NewGuid().ToString("N");
    }
}
