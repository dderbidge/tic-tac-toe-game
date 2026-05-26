using System;
using System.Collections.Generic;

namespace TicTacToe.WebMultiplayer.Services;

public sealed record PlayerSession(
    string ConnectionId,
    string Name,
    DateTimeOffset ConnectedAt
);

public sealed class PresenceTracker
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PlayerSession> _sessions = new();
    private readonly Dictionary<string, string> _matchByConnection = new();

    public event Action? Changed;

    public PlayerSession Add(string connectionId)
    {
        PlayerSession session;
        lock (_gate)
        {
            var baseName = NameGenerator.For(connectionId);
            var name = baseName;
            int suffix = 2;
            while (NameTaken(name)) name = $"{baseName} {suffix++}";

            session = new PlayerSession(connectionId, name, DateTimeOffset.UtcNow);
            _sessions[connectionId] = session;
        }
        Changed?.Invoke();
        return session;
    }

    public PlayerSession? Remove(string connectionId)
    {
        PlayerSession? removed = null;
        lock (_gate)
        {
            if (_sessions.Remove(connectionId, out var s)) removed = s;
            _matchByConnection.Remove(connectionId);
        }
        if (removed != null) Changed?.Invoke();
        return removed;
    }

    public PlayerSession? Get(string connectionId)
    {
        lock (_gate) return _sessions.TryGetValue(connectionId, out var s) ? s : null;
    }

    public IReadOnlyList<PlayerSession> SnapshotLobby()
    {
        lock (_gate)
        {
            var list = new List<PlayerSession>(_sessions.Count);
            foreach (var kv in _sessions)
                if (!_matchByConnection.ContainsKey(kv.Key))
                    list.Add(kv.Value);
            return list;
        }
    }

    public void EnterMatch(string connectionId, string matchId)
    {
        lock (_gate) _matchByConnection[connectionId] = matchId;
        Changed?.Invoke();
    }

    public void LeaveMatch(string connectionId)
    {
        lock (_gate) _matchByConnection.Remove(connectionId);
        Changed?.Invoke();
    }

    public string? CurrentMatchOf(string connectionId)
    {
        lock (_gate) return _matchByConnection.TryGetValue(connectionId, out var id) ? id : null;
    }

    private bool NameTaken(string name)
    {
        // _gate already held by caller.
        foreach (var s in _sessions.Values)
            if (s.Name == name) return true;
        return false;
    }
}
