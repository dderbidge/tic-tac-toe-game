using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using TicTacToe.Core;

namespace TicTacToe.WebMultiplayer.Services;

public sealed class MultiplayerMatch
{
    public required string MatchId { get; init; }
    public required string AscendantConnId { get; set; }
    public required string VanguardConnId { get; set; }
    public required string AscendantName { get; init; }
    public required string VanguardName { get; init; }
    public Board Board { get; } = new();
    public Player Active { get; set; } = Player.Ascendant;
    public Player? Winner { get; set; }
    public bool IsDraw { get; set; }
    public (int row, int col)[]? WinningCells { get; set; }
    public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;
    // Credentials are delivered only in the corresponding player's acceptance message.
    [JsonIgnore]
    public string? AscendantReconnectToken { get; internal set; } = NewReconnectToken();
    [JsonIgnore]
    public string? VanguardReconnectToken { get; internal set; } = NewReconnectToken();
    internal Dictionary<string, DisconnectLease> Disconnects { get; } = new();

    private static string NewReconnectToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}

internal sealed record DisconnectLease(Guid Id, DateTimeOffset ExpiresAt, ITimer Timer);

public sealed record PlaceResult(bool Accepted, MultiplayerMatch? Match, string? Reason);

public sealed class MatchRegistry : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, MultiplayerMatch> _matches = new();
    private readonly TimeProvider _clock;

    public MatchRegistry(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    public MultiplayerMatch Create(string ascConnId, string ascName, string vanConnId, string vanName)
    {
        var match = new MultiplayerMatch
        {
            MatchId = Guid.NewGuid().ToString("N")[..10],
            AscendantConnId = ascConnId,
            VanguardConnId = vanConnId,
            AscendantName = ascName,
            VanguardName = vanName,
        };
        lock (_gate) _matches[match.MatchId] = match;
        return match;
    }

    public MultiplayerMatch? Get(string matchId)
    {
        lock (_gate) return _matches.TryGetValue(matchId, out var m) ? m : null;
    }

    public PlaceResult TryPlace(string matchId, string fromConnId, int row, int col)
    {
        lock (_gate)
        {
            if (!_matches.TryGetValue(matchId, out var m))
                return new PlaceResult(false, null, "no such match");
            if (m.Winner != null || m.IsDraw)
                return new PlaceResult(false, m, "match already ended");
            var fromFaction = m.AscendantConnId == fromConnId ? Player.Ascendant
                            : m.VanguardConnId  == fromConnId ? Player.Vanguard
                            : Player.None;
            if (fromFaction == Player.None)
                return new PlaceResult(false, m, "not a participant");
            if (m.Disconnects.ContainsKey(fromConnId))
                return new PlaceResult(false, m, "player disconnected");
            if (m.Active != fromFaction)
                return new PlaceResult(false, m, "not your turn");
            if (!m.Board.TryPlace(row, col, fromFaction))
                return new PlaceResult(false, m, "invalid cell");

            m.LastActivityAt = DateTimeOffset.UtcNow;

            var winner = m.Board.Winner();
            if (winner is { } w)
            {
                m.Winner = w;
                m.WinningCells = ComputeWinningCells(m.Board);
            }
            else if (m.Board.IsDraw())
            {
                m.IsDraw = true;
            }
            else
            {
                m.Active = Board.Other(m.Active);
            }
            if (m.Winner is not null || m.IsDraw) ClearReconnectState(m);
            return new PlaceResult(true, m, null);
        }
    }

    public MultiplayerMatch? Forfeit(string matchId, string forfeiterConnId)
    {
        lock (_gate)
        {
            if (!_matches.TryGetValue(matchId, out var m)) return null;
            if (m.AscendantConnId != forfeiterConnId && m.VanguardConnId != forfeiterConnId) return null;
            if (m.Winner != null || m.IsDraw) return null;
            m.Winner = m.AscendantConnId == forfeiterConnId ? Player.Vanguard : Player.Ascendant;
            ClearReconnectState(m);
            return m;
        }
    }

    public MultiplayerMatch? MarkDisconnected(string matchId, string connectionId, TimeSpan gracePeriod,
        Action<string, string, Guid> onExpired)
        => MarkDisconnected(matchId, connectionId, gracePeriod, onExpired, null);

    internal MultiplayerMatch? MarkDisconnected(string matchId, string connectionId, TimeSpan gracePeriod,
        Action<string, string, Guid> onExpired, Action<string>? notifyOpponent)
    {
        lock (_gate)
        {
            if (!_matches.TryGetValue(matchId, out var m) || m.Winner is not null || m.IsDraw) return null;
            if (m.AscendantConnId != connectionId && m.VanguardConnId != connectionId) return null;
            // Duplicate disconnect notifications must not extend the grace period.
            if (m.Disconnects.ContainsKey(connectionId)) return null;
            var leaseId = Guid.NewGuid();
            var expiresAt = _clock.GetUtcNow() + gracePeriod;
            var timer = _clock.CreateTimer(_ => onExpired(matchId, connectionId, leaseId), null,
                gracePeriod, Timeout.InfiniteTimeSpan);
            m.Disconnects[connectionId] = new DisconnectLease(leaseId, expiresAt, timer);
            // Queue this event before a rejoin can queue its matching reconnect event.
            notifyOpponent?.Invoke(m.AscendantConnId == connectionId ? m.VanguardConnId : m.AscendantConnId);
            return m;
        }
    }

    public MultiplayerMatch? TryRejoin(string matchId, string connectionId, string? reconnectToken)
        => TryRejoin(matchId, connectionId, reconnectToken, null);

    // Restore presence before releasing the identity lock so a simultaneous
    // disconnect or forfeit cannot leave a stale presence entry behind.
    internal MultiplayerMatch? TryRejoin(string matchId, string connectionId, string? reconnectToken,
        Action<MultiplayerMatch>? restorePresence)
    {
        lock (_gate)
        {
            if (!_matches.TryGetValue(matchId, out var m) || m.Winner is not null || m.IsDraw) return null;
            var isAscendant = TokenMatches(m.AscendantReconnectToken, reconnectToken);
            if (!isAscendant && !TokenMatches(m.VanguardReconnectToken, reconnectToken)) return null;
            var oldConnectionId = isAscendant ? m.AscendantConnId : m.VanguardConnId;
            // A connection must not occupy both seats or join a second match.
            if (_matches.Values.Any(other => other != m &&
                (other.AscendantConnId == connectionId || other.VanguardConnId == connectionId))) return null;
            if (connectionId == (isAscendant ? m.VanguardConnId : m.AscendantConnId)) return null;
            // Retrying a successful invocation is safe if its response was lost.
            if (oldConnectionId == connectionId && !m.Disconnects.ContainsKey(oldConnectionId))
            {
                restorePresence?.Invoke(m);
                return m;
            }
            if (!m.Disconnects.TryGetValue(oldConnectionId, out var lease) ||
                _clock.GetUtcNow() >= lease.ExpiresAt) return null;

            m.Disconnects.Remove(oldConnectionId);
            lease.Timer.Dispose();
            if (isAscendant) m.AscendantConnId = connectionId;
            else m.VanguardConnId = connectionId;
            restorePresence?.Invoke(m);
            return m;
        }
    }

    public MultiplayerMatch? ForfeitDisconnected(string matchId, string connectionId, Guid disconnectId)
    {
        lock (_gate)
        {
            if (!_matches.TryGetValue(matchId, out var m) || m.Winner is not null || m.IsDraw) return null;
            if (!m.Disconnects.TryGetValue(connectionId, out var lease) || lease.Id != disconnectId ||
                _clock.GetUtcNow() < lease.ExpiresAt) return null;
            if (m.AscendantConnId != connectionId && m.VanguardConnId != connectionId) return null;
            m.Winner = m.AscendantConnId == connectionId ? Player.Vanguard : Player.Ascendant;
            ClearReconnectState(m);
            return m;
        }
    }

    public bool Remove(string matchId)
    {
        lock (_gate)
        {
            if (!_matches.Remove(matchId, out var m)) return false;
            ClearReconnectState(m);
            return true;
        }
    }

    private static bool TokenMatches(string? expected, string? supplied) =>
        expected is not null && supplied is not null && supplied.Length == expected.Length &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));

    private static void ClearReconnectState(MultiplayerMatch m)
    {
        m.AscendantReconnectToken = null;
        m.VanguardReconnectToken = null;
        foreach (var lease in m.Disconnects.Values) lease.Timer.Dispose();
        m.Disconnects.Clear();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var match in _matches.Values) ClearReconnectState(match);
            _matches.Clear();
        }
    }

    public IEnumerable<string> ActiveMatchesContaining(string connectionId)
    {
        lock (_gate)
        {
            var ids = new List<string>();
            foreach (var m in _matches.Values)
                if (m.AscendantConnId == connectionId || m.VanguardConnId == connectionId)
                    ids.Add(m.MatchId);
            return ids;
        }
    }

    private static (int row, int col)[]? ComputeWinningCells(Board b)
    {
        for (int r = 0; r < 3; r++)
            if (b.At(r, 0) != Player.None && b.At(r, 0) == b.At(r, 1) && b.At(r, 1) == b.At(r, 2))
                return new[] { (r, 0), (r, 1), (r, 2) };
        for (int c = 0; c < 3; c++)
            if (b.At(0, c) != Player.None && b.At(0, c) == b.At(1, c) && b.At(1, c) == b.At(2, c))
                return new[] { (0, c), (1, c), (2, c) };
        if (b.At(0, 0) != Player.None && b.At(0, 0) == b.At(1, 1) && b.At(1, 1) == b.At(2, 2))
            return new[] { (0, 0), (1, 1), (2, 2) };
        if (b.At(0, 2) != Player.None && b.At(0, 2) == b.At(1, 1) && b.At(1, 1) == b.At(2, 0))
            return new[] { (0, 2), (1, 1), (2, 0) };
        return null;
    }
}
