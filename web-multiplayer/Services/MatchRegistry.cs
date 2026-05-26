using System;
using System.Collections.Generic;
using System.Linq;
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
    public string? DisconnectedConnId { get; set; }
    public Timer? DisconnectTimer { get; set; }
}

public sealed record PlaceResult(bool Accepted, MultiplayerMatch? Match, string? Reason);

public sealed class MatchRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, MultiplayerMatch> _matches = new();

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
            return new PlaceResult(true, m, null);
        }
    }

    public MultiplayerMatch? Forfeit(string matchId, string forfeiterConnId)
    {
        lock (_gate)
        {
            if (!_matches.TryGetValue(matchId, out var m)) return null;
            if (m.Winner != null || m.IsDraw) return m;
            m.Winner = m.AscendantConnId == forfeiterConnId ? Player.Vanguard : Player.Ascendant;
            return m;
        }
    }

    public bool Remove(string matchId)
    {
        lock (_gate) return _matches.Remove(matchId);
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
