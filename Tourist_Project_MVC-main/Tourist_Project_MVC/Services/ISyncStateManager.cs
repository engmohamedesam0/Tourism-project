using System;
using System.Threading;

namespace Tourist_Project_MVC.Services;

public enum SyncState
{
    Idle,
    Syncing,
    Pulling,
    Failed
}

public interface ISyncStateManager
{
    SyncState State { get; }
    DateTime? LastSyncTimeUtc { get; }
    DateTime? LastPullTimeUtc { get; }
    ArcGISSyncResult? LastSyncResult { get; }
    ArcGISSyncResult? LastPullResult { get; }

    bool TryBeginSync(out SyncState currentStatus);
    bool TryBeginPull(out SyncState currentStatus);
    void EndOperation(ArcGISSyncResult result, bool isSync);
    SyncStatusDto GetStatus();
}

public record SyncStatusDto(
    string State,
    DateTime? LastSyncTimeUtc,
    DateTime? LastPullTimeUtc,
    ArcGISSyncResult? LastSyncResult,
    ArcGISSyncResult? LastPullResult);

public class SyncStateManager : ISyncStateManager
{
    private readonly object _lock = new();
    private SyncState _state = SyncState.Idle;
    private DateTime? _lastSyncTimeUtc;
    private DateTime? _lastPullTimeUtc;
    private ArcGISSyncResult? _lastSyncResult;
    private ArcGISSyncResult? _lastPullResult;

    public SyncState State
    {
        get
        {
            lock (_lock) return _state;
        }
    }

    public DateTime? LastSyncTimeUtc
    {
        get
        {
            lock (_lock) return _lastSyncTimeUtc;
        }
    }

    public DateTime? LastPullTimeUtc
    {
        get
        {
            lock (_lock) return _lastPullTimeUtc;
        }
    }

    public ArcGISSyncResult? LastSyncResult
    {
        get
        {
            lock (_lock) return _lastSyncResult;
        }
    }

    public ArcGISSyncResult? LastPullResult
    {
        get
        {
            lock (_lock) return _lastPullResult;
        }
    }

    public bool TryBeginSync(out SyncState currentStatus)
    {
        lock (_lock)
        {
            currentStatus = _state;
            if (_state != SyncState.Idle && _state != SyncState.Failed)
            {
                return false;
            }
            _state = SyncState.Syncing;
            return true;
        }
    }

    public bool TryBeginPull(out SyncState currentStatus)
    {
        lock (_lock)
        {
            currentStatus = _state;
            if (_state != SyncState.Idle && _state != SyncState.Failed)
            {
                return false;
            }
            _state = SyncState.Pulling;
            return true;
        }
    }

    public void EndOperation(ArcGISSyncResult result, bool isSync)
    {
        lock (_lock)
        {
            _state = result.Success ? SyncState.Idle : SyncState.Failed;
            if (isSync)
            {
                _lastSyncResult = result;
                if (result.Success)
                {
                    _lastSyncTimeUtc = DateTime.UtcNow;
                }
            }
            else
            {
                _lastPullResult = result;
                if (result.Success)
                {
                    _lastPullTimeUtc = DateTime.UtcNow;
                }
            }
        }
    }

    public SyncStatusDto GetStatus()
    {
        lock (_lock)
        {
            return new SyncStatusDto(
                _state.ToString(),
                _lastSyncTimeUtc,
                _lastPullTimeUtc,
                _lastSyncResult,
                _lastPullResult);
        }
    }
}
