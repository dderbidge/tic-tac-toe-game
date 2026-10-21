using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using TicTacToe.Core;
using TicTacToe.WebMultiplayer.Services;

namespace TicTacToe.WebMultiplayer.Hubs;

public class GameHub : Hub
{
    private const string LobbyGroup = "lobby";
    private const int DisconnectGraceSeconds = 30;

    private readonly PresenceTracker _presence;
    private readonly MatchRegistry _matches;
    private readonly ChallengeBroker _challenges;
    private readonly IHubContext<GameHub> _hub;

    public GameHub(
        PresenceTracker presence,
        MatchRegistry matches,
        ChallengeBroker challenges,
        IHubContext<GameHub> hub)
    {
        _presence = presence;
        _matches = matches;
        _challenges = challenges;
        _hub = hub;
    }

    // ─── Lifecycle ─────────────────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        // Presence is created on EnterLobby, not on connect.
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connId = Context.ConnectionId;

        // Match membership is authoritative, including if lobby presence was removed earlier.
        var notifications = new List<Task>();
        foreach (var id in _matches.ActiveMatchesContaining(connId))
            _matches.MarkDisconnected(id, connId, TimeSpan.FromSeconds(DisconnectGraceSeconds), OnGraceExpired,
                partnerConnId => notifications.Add(_hub.Clients.Client(partnerConnId)
                    .SendAsync("OnOpponentDisconnected", new { secondsLeft = DisconnectGraceSeconds })));
        _presence.Remove(connId);
        _challenges.RemoveAllInvolving(connId);
        await Task.WhenAll(notifications);
        await Clients.Group(LobbyGroup).SendAsync("OnPlayerLeft", new { connId });
        await base.OnDisconnectedAsync(exception);
    }

    // ─── Lobby ─────────────────────────────────────────────────────

    public async Task EnterLobby()
    {
        var connId = Context.ConnectionId;
        if (_presence.Get(connId) is null)
            _presence.Add(connId);
        await Groups.AddToGroupAsync(connId, LobbyGroup);

        var me = _presence.Get(connId)!;
        var others = _presence.SnapshotLobby();
        await Clients.Caller.SendAsync("OnLobbyState", new
        {
            yourConnId = connId,
            yourName = me.Name,
            players = others
        });
        await Clients.OthersInGroup(LobbyGroup).SendAsync("OnPlayerJoined", new { connId, name = me.Name });
    }

    public async Task LeaveLobby()
    {
        var connId = Context.ConnectionId;
        await Groups.RemoveFromGroupAsync(connId, LobbyGroup);
        _presence.Remove(connId);
        _challenges.RemoveAllInvolving(connId);
        await Clients.Group(LobbyGroup).SendAsync("OnPlayerLeft", new { connId });
    }

    // ─── Challenges ────────────────────────────────────────────────

    public async Task ChallengePlayer(string targetConnectionId)
    {
        var fromConnId = Context.ConnectionId;
        var from = _presence.Get(fromConnId);
        var to = _presence.Get(targetConnectionId);
        if (from is null || to is null) return;
        if (_presence.CurrentMatchOf(fromConnId) is not null) return;
        if (_presence.CurrentMatchOf(targetConnectionId) is not null) return;

        var c = _challenges.Create(fromConnId, from.Name, targetConnectionId, to.Name);
        await Clients.Client(targetConnectionId).SendAsync("OnIncomingChallenge", new
        {
            challengeId = c.ChallengeId,
            fromConnId,
            fromName = from.Name
        });
    }

    public async Task RespondToChallenge(string challengeId, bool accept)
    {
        var c = _challenges.Take(challengeId);
        if (c is null) return;
        if (c.ToConnId != Context.ConnectionId) return;

        if (!accept)
        {
            await Clients.Client(c.FromConnId).SendAsync("OnChallengeDeclined", new { fromName = c.ToName });
            return;
        }

        var match = _matches.Create(c.FromConnId, c.FromName, c.ToConnId, c.ToName);
        _presence.EnterMatch(c.FromConnId, match.MatchId);
        _presence.EnterMatch(c.ToConnId, match.MatchId);
        await Clients.Group(LobbyGroup).SendAsync("OnPlayerLeft", new { connId = c.FromConnId });
        await Clients.Group(LobbyGroup).SendAsync("OnPlayerLeft", new { connId = c.ToConnId });

        await Clients.Client(c.FromConnId).SendAsync("OnChallengeAccepted", new
        {
            matchId = match.MatchId,
            opponentName = c.ToName,
            yourFaction = "Ascendant",
            reconnectToken = match.AscendantReconnectToken
        });
        await Clients.Client(c.ToConnId).SendAsync("OnChallengeAccepted", new
        {
            matchId = match.MatchId,
            opponentName = c.FromName,
            yourFaction = "Vanguard",
            reconnectToken = match.VanguardReconnectToken
        });

        await BroadcastMatchState(match);
    }

    public Task CancelChallenge(string challengeId)
    {
        _challenges.Cancel(challengeId, Context.ConnectionId);
        return Task.CompletedTask;
    }

    // ─── Match ─────────────────────────────────────────────────────

    public async Task PlaceMove(string matchId, int row, int col)
    {
        var connId = Context.ConnectionId;
        var result = _matches.TryPlace(matchId, connId, row, col);
        if (!result.Accepted || result.Match is null) return;

        await BroadcastMatchState(result.Match);

        if (result.Match.Winner is not null || result.Match.IsDraw)
        {
            await BroadcastMatchEnded(result.Match, reason: result.Match.IsDraw ? "draw" : "win");
            EndMatchAndReturnPlayersToLobby(result.Match);
        }
    }

    public async Task ForfeitMatch(string matchId)
    {
        var m = _matches.Forfeit(matchId, Context.ConnectionId);
        if (m is null) return;
        await BroadcastMatchEnded(m, reason: "forfeit");
        EndMatchAndReturnPlayersToLobby(m);
    }

    public Task RequestRematch(string matchId)
    {
        // v1: clients return to lobby and re-challenge. Method exists for forward compat.
        return Task.CompletedTask;
    }

    public async Task<bool> RejoinMatch(string matchId, string? reconnectToken)
    {
        var connId = Context.ConnectionId;
        Task reconnectedNotification = Task.CompletedTask;
        var m = _matches.TryRejoin(matchId, connId, reconnectToken, match =>
        {
            var name = match.AscendantConnId == connId ? match.AscendantName : match.VanguardName;
            _presence.RestoreMatch(connId, name, match.MatchId);
            var partnerConnId = match.AscendantConnId == connId ? match.VanguardConnId : match.AscendantConnId;
            reconnectedNotification = _hub.Clients.Client(partnerConnId).SendAsync("OnOpponentReconnected");
        });
        if (m is null) return false;

        await Clients.Group(LobbyGroup).SendAsync("OnPlayerLeft", new { connId });
        await reconnectedNotification;
        await BroadcastMatchState(m);
        return true;
    }

    // ─── Helpers ───────────────────────────────────────────────────

    private async Task BroadcastMatchState(MultiplayerMatch m)
    {
        var payload = new
        {
            matchId = m.MatchId,
            board = SerializeBoard(m.Board),
            activeFaction = m.Active.ToString(),
            winner = m.Winner?.ToString(),
            isDraw = m.IsDraw,
            winningCells = m.WinningCells?.Select(c => new { c.row, c.col }).ToArray()
        };
        await _hub.Clients.Client(m.AscendantConnId).SendAsync("OnMatchState", payload);
        await _hub.Clients.Client(m.VanguardConnId).SendAsync("OnMatchState", payload);
    }

    private async Task BroadcastMatchEnded(MultiplayerMatch m, string reason)
    {
        await _hub.Clients.Client(m.AscendantConnId).SendAsync("OnMatchEnded", new
        {
            matchId = m.MatchId,
            result = m.Winner == Player.Ascendant ? "win" : (m.Winner == Player.Vanguard ? "loss" : "draw"),
            reason,
            winningCells = m.WinningCells?.Select(c => new { c.row, c.col }).ToArray()
        });
        await _hub.Clients.Client(m.VanguardConnId).SendAsync("OnMatchEnded", new
        {
            matchId = m.MatchId,
            result = m.Winner == Player.Vanguard ? "win" : (m.Winner == Player.Ascendant ? "loss" : "draw"),
            reason,
            winningCells = m.WinningCells?.Select(c => new { c.row, c.col }).ToArray()
        });
    }

    private void EndMatchAndReturnPlayersToLobby(MultiplayerMatch m)
    {
        _presence.LeaveMatch(m.AscendantConnId);
        _presence.LeaveMatch(m.VanguardConnId);
        _matches.Remove(m.MatchId);
    }

    private void OnGraceExpired(string matchId, string disconnectedConnId, Guid disconnectId)
    {
        var m = _matches.ForfeitDisconnected(matchId, disconnectedConnId, disconnectId);
        if (m is null) return;
        try
        {
            BroadcastMatchEnded(m, reason: "forfeit").GetAwaiter().GetResult();
        }
        finally
        {
            EndMatchAndReturnPlayersToLobby(m);
        }
    }

    private static int[][] SerializeBoard(Board b)
    {
        var rows = new int[3][];
        for (int r = 0; r < 3; r++)
        {
            rows[r] = new int[3];
            for (int c = 0; c < 3; c++) rows[r][c] = (int)b.At(r, c);
        }
        return rows;
    }
}
