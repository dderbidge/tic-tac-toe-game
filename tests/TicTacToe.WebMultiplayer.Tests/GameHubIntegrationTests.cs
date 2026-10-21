using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TicTacToe.Core;
using TicTacToe.WebMultiplayer.Hubs;
using TicTacToe.WebMultiplayer.Services;
using Xunit;

namespace TicTacToe.WebMultiplayer.Tests;

// These tests use a real loopback Kestrel server and the actual SignalR JSON protocol.
public sealed class GameHubIntegrationTests
{
    [Fact]
    public async Task Acceptance_sends_each_player_only_their_token_and_shared_state_contains_no_credentials()
    {
        await using var server = await LiveServer.StartAsync();
        await using var asc = await server.JoinAsync();
        await using var van = await server.JoinAsync();
        await using var outsider = await server.JoinAsync();
        var started = await StartMatchAsync(asc, van);
        var match = server.Matches.Get(started.MatchId)!;

        Assert.Equal(match.AscendantReconnectToken, started.AscToken);
        Assert.Equal(match.VanguardReconnectToken, started.VanToken);
        Assert.NotEqual(started.AscToken, started.VanToken);
        Assert.Equal("Ascendant", started.AscPayload.GetProperty("yourFaction").GetString());
        Assert.Equal("Vanguard", started.VanPayload.GetProperty("yourFaction").GetString());
        Assert.All(asc.Received, message => Assert.DoesNotContain(started.VanToken, message.Payload.ToString()));
        Assert.All(van.Received, message => Assert.DoesNotContain(started.AscToken, message.Payload.ToString()));
        Assert.All(outsider.Received, message =>
        {
            Assert.DoesNotContain(started.AscToken, message.Payload.ToString());
            Assert.DoesNotContain(started.VanToken, message.Payload.ToString());
        });
        var ascState = await asc.NextAsync("OnMatchState");
        var vanState = await van.NextAsync("OnMatchState");
        Assert.DoesNotContain("reconnectToken", ascState.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reconnectToken", vanState.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejoin_rejects_missing_wrong_and_opponent_tokens_then_restores_gameplay_with_the_valid_token()
    {
        await using var server = await LiveServer.StartAsync();
        await using var asc = await server.JoinAsync();
        await using var van = await server.JoinAsync();
        var originalName = server.Presence.Get(asc.ConnectionId)!.Name;
        var started = await StartMatchAsync(asc, van);
        await asc.NextAsync("OnMatchState");
        await van.NextAsync("OnMatchState");
        await asc.Connection.InvokeAsync("PlaceMove", started.MatchId, 0, 0);
        await asc.NextAsync("OnMatchState");
        await van.NextAsync("OnMatchState");
        await asc.Connection.StopAsync();
        await van.NextAsync("OnOpponentDisconnected");
        await using var returning = await server.JoinAsync();

        await Assert.ThrowsAsync<HubException>(() => returning.Connection.InvokeAsync<bool>("RejoinMatch", started.MatchId));
        Assert.False(await returning.Connection.InvokeAsync<bool>("RejoinMatch", started.MatchId, (string?)null));
        Assert.False(await returning.Connection.InvokeAsync<bool>("RejoinMatch", started.MatchId, ""));
        Assert.False(await returning.Connection.InvokeAsync<bool>("RejoinMatch", started.MatchId, new string('0', 64)));
        Assert.False(await returning.Connection.InvokeAsync<bool>("RejoinMatch", started.MatchId, started.VanToken));
        Assert.Null(server.Presence.CurrentMatchOf(returning.ConnectionId));
        Assert.Equal(asc.ConnectionId, server.Matches.Get(started.MatchId)!.AscendantConnId);

        Assert.True(await returning.Connection.InvokeAsync<bool>("RejoinMatch", started.MatchId, started.AscToken));
        await van.NextAsync("OnOpponentReconnected");
        var restored = await returning.NextAsync("OnMatchState");
        await van.NextAsync("OnMatchState");
        Assert.Equal(started.MatchId, server.Presence.CurrentMatchOf(returning.ConnectionId));
        Assert.Equal(originalName, server.Presence.Get(returning.ConnectionId)!.Name);
        Assert.Null(server.Presence.Get(asc.ConnectionId));
        Assert.Equal(returning.ConnectionId, server.Matches.Get(started.MatchId)!.AscendantConnId);
        Assert.Equal((int)Player.Ascendant, restored.GetProperty("board")[0][0].GetInt32());
        Assert.Equal("Vanguard", restored.GetProperty("activeFaction").GetString());
        Assert.False(server.Matches.TryPlace(started.MatchId, asc.ConnectionId, 2, 2).Accepted);
        Assert.Null(server.Matches.Forfeit(started.MatchId, asc.ConnectionId));

        await van.Connection.InvokeAsync("PlaceMove", started.MatchId, 1, 1);
        await returning.NextAsync("OnMatchState");
        await van.NextAsync("OnMatchState");
        await returning.Connection.InvokeAsync("PlaceMove", started.MatchId, 0, 1);
        var continued = await returning.NextAsync("OnMatchState");
        await van.NextAsync("OnMatchState");
        Assert.Equal((int)Player.Ascendant, continued.GetProperty("board")[0][1].GetInt32());
        Assert.Null(server.Matches.Get(started.MatchId)!.Winner);
    }

    [Fact]
    public async Task Outsider_forfeit_has_no_effect_and_participant_forfeit_ends_and_cleans_up_the_match()
    {
        await using var server = await LiveServer.StartAsync();
        await using var asc = await server.JoinAsync();
        await using var van = await server.JoinAsync();
        await using var outsider = await server.JoinAsync();
        var started = await StartMatchAsync(asc, van);
        var match = server.Matches.Get(started.MatchId)!;
        await asc.NextAsync("OnMatchState");
        await van.NextAsync("OnMatchState");

        await outsider.Connection.InvokeAsync("ForfeitMatch", started.MatchId);
        Assert.Same(match, server.Matches.Get(started.MatchId));
        Assert.Null(match.Winner);
        Assert.Equal(started.AscToken, match.AscendantReconnectToken);
        Assert.Equal(started.MatchId, server.Presence.CurrentMatchOf(asc.ConnectionId));
        Assert.Equal(started.MatchId, server.Presence.CurrentMatchOf(van.ConnectionId));
        Assert.DoesNotContain(asc.Received, message => message.Method == "OnMatchEnded");
        Assert.DoesNotContain(van.Received, message => message.Method == "OnMatchEnded");

        await van.Connection.InvokeAsync("ForfeitMatch", started.MatchId);
        var ascEnded = await asc.NextAsync("OnMatchEnded");
        var vanEnded = await van.NextAsync("OnMatchEnded");
        Assert.Equal("win", ascEnded.GetProperty("result").GetString());
        Assert.Equal("loss", vanEnded.GetProperty("result").GetString());
        Assert.Equal("forfeit", ascEnded.GetProperty("reason").GetString());
        Assert.Null(server.Matches.Get(started.MatchId));
        Assert.Null(server.Presence.CurrentMatchOf(asc.ConnectionId));
        Assert.Null(server.Presence.CurrentMatchOf(van.ConnectionId));
        Assert.Null(match.AscendantReconnectToken);
        Assert.Null(match.VanguardReconnectToken);
        Assert.False(await outsider.Connection.InvokeAsync<bool>("RejoinMatch", started.MatchId, started.AscToken));
    }

    [Fact]
    public async Task Disconnect_timeout_ends_match_and_saved_token_cannot_revive_it()
    {
        await using var server = await LiveServer.StartAsync();
        await using var asc = await server.JoinAsync();
        await using var van = await server.JoinAsync();
        var started = await StartMatchAsync(asc, van);
        await asc.NextAsync("OnMatchState");
        await van.NextAsync("OnMatchState");
        await asc.Connection.StopAsync();
        await van.NextAsync("OnOpponentDisconnected");
        var timer = server.Clock.LastTimer;
        server.Clock.Advance(TimeSpan.FromSeconds(30));
        timer.FireQueuedCallback();
        var ended = await van.NextAsync("OnMatchEnded");
        Assert.Equal("win", ended.GetProperty("result").GetString());
        Assert.Equal("forfeit", ended.GetProperty("reason").GetString());
        Assert.Null(server.Matches.Get(started.MatchId));
        await using var returning = await server.JoinAsync();
        Assert.False(await returning.Connection.InvokeAsync<bool>("RejoinMatch", started.MatchId, started.AscToken));
        Assert.Null(server.Presence.CurrentMatchOf(returning.ConnectionId));
    }

    private static async Task<StartedMatch> StartMatchAsync(PlayerClient asc, PlayerClient van)
    {
        await asc.Connection.InvokeAsync("ChallengePlayer", van.ConnectionId);
        var incoming = await van.NextAsync("OnIncomingChallenge");
        await van.Connection.InvokeAsync("RespondToChallenge", incoming.GetProperty("challengeId").GetString()!, true);
        var ascAccepted = await asc.NextAsync("OnChallengeAccepted");
        var vanAccepted = await van.NextAsync("OnChallengeAccepted");
        var matchId = ascAccepted.GetProperty("matchId").GetString()!;
        Assert.Equal(matchId, vanAccepted.GetProperty("matchId").GetString());
        return new StartedMatch(matchId, ascAccepted.GetProperty("reconnectToken").GetString()!,
            vanAccepted.GetProperty("reconnectToken").GetString()!, ascAccepted, vanAccepted);
    }

    private sealed record StartedMatch(string MatchId, string AscToken, string VanToken, JsonElement AscPayload, JsonElement VanPayload);

    private sealed class LiveServer(WebApplication app, string endpoint, ManualTimeProvider clock) : IAsyncDisposable
    {
        public ManualTimeProvider Clock { get; } = clock;
        public MatchRegistry Matches => app.Services.GetRequiredService<MatchRegistry>();
        public PresenceTracker Presence => app.Services.GetRequiredService<PresenceTracker>();

        public static async Task<LiveServer> StartAsync()
        {
            var clock = new ManualTimeProvider();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Services.AddSignalR();
            builder.Services.AddSingleton<PresenceTracker>();
            builder.Services.AddSingleton(new MatchRegistry(clock));
            builder.Services.AddSingleton<ChallengeBroker>();
            var app = builder.Build();
            app.MapHub<GameHub>("/gamehub");
            await app.StartAsync();
            var server = app.Services.GetRequiredService<IServer>();
            var address = Assert.Single(server.Features.Get<IServerAddressesFeature>()!.Addresses);
            return new LiveServer(app, address + "/gamehub", clock);
        }

        public async Task<PlayerClient> JoinAsync()
        {
            var player = new PlayerClient(new HubConnectionBuilder().WithUrl(endpoint).Build());
            await player.Connection.StartAsync();
            await player.Connection.InvokeAsync("EnterLobby");
            var state = await player.NextAsync("OnLobbyState");
            player.ConnectionId = state.GetProperty("yourConnId").GetString()!;
            return player;
        }

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private sealed record WireMessage(string Method, JsonElement Payload);

    private sealed class PlayerClient : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, Channel<JsonElement>> _messages = new();
        public HubConnection Connection { get; }
        public string ConnectionId { get; set; } = "";
        public ConcurrentQueue<WireMessage> Received { get; } = new();

        public PlayerClient(HubConnection connection)
        {
            Connection = connection;
            foreach (var method in new[] { "OnLobbyState", "OnPlayerJoined", "OnPlayerLeft", "OnIncomingChallenge",
                "OnChallengeAccepted", "OnMatchState", "OnMatchEnded", "OnOpponentDisconnected" })
            {
                var name = method;
                _messages[name] = Channel.CreateUnbounded<JsonElement>();
                connection.On<JsonElement>(name, payload => Record(name, payload));
            }
            _messages["OnOpponentReconnected"] = Channel.CreateUnbounded<JsonElement>();
            connection.On("OnOpponentReconnected", () => Record("OnOpponentReconnected", default));
        }

        private void Record(string method, JsonElement payload)
        {
            Received.Enqueue(new WireMessage(method, payload));
            _messages[method].Writer.TryWrite(payload);
        }

        public async Task<JsonElement> NextAsync(string method)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            return await _messages[method].Reader.ReadAsync(timeout.Token);
        }

        public async ValueTask DisposeAsync() => await Connection.DisposeAsync();
    }
}
