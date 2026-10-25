using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TicTacToe.Core;
using TicTacToe.WebMultiplayer.Hubs;
using TicTacToe.WebMultiplayer.Services;
using Xunit;
using Xunit.Abstractions;

namespace TicTacToe.WebMultiplayer.Tests;

// Finite gameplay agents, separate from production opponent AI. These drive the
// real session and SignalR interfaces; they do not render or inspect the browser.
public sealed class GameplayPlaytesterTests(ITestOutputHelper output)
{
    private static readonly int[][] Lines = [[0,1,2], [3,4,5], [6,7,8], [0,3,6], [1,4,7], [2,5,8], [0,4,8], [2,4,6]];

    [Theory]
    [InlineData(13)]
    [InlineData(2026)]
    [InlineData(104729)]
    public async Task Seeded_local_players_finish_replay_reset_and_recover_from_menu(int seed)
    {
        var trace = new List<string>();
        var watch = Stopwatch.StartNew();
        try
        {
            foreach (var tactical in new[] { false, true })
            {
                var rng = new Random(seed);
                var game = new LocalGameSession();
                var isolated = new LocalGameSession();
                isolated.StartHotSeat();
                game.StartHotSeat();
                // A player changes their mind partway through a round, then restarts.
                await game.TryHumanMoveAsync(0, 0);
                game.GoToMainMenu();
                await game.TryHumanMoveAsync(1, 1);
                Check(Cells(game.Board).Count(x => x != 0) == 1, "menu accepted a move");
                game.StartHotSeat();
                for (var round = 0; round < 4; round++)
                {
                    trace.Add($"local profile={(tactical ? "tactical" : "random")} round={round}");
                    var moves = 0;
                    while (game.Phase == LocalPhase.InMatch && moves < 9)
                    {
                        var before = Cells(game.Board);
                        await game.TryHumanMoveAsync(-1, 3);
                        Check(before.SequenceEqual(Cells(game.Board)), "out-of-range move changed board");
                        var move = Choose(before, (int)game.Active, rng, tactical);
                        trace.Add($"{game.Active}:{move / 3},{move % 3} board={string.Join("", before)}");
                        await game.TryHumanMoveAsync(move / 3, move % 3);
                        var after = Cells(game.Board);
                        Check(before.Zip(after).Count(pair => pair.First != pair.Second) == 1, "move changed more than one cell");
                        await game.TryHumanMoveAsync(move / 3, move % 3);
                        Check(after.SequenceEqual(Cells(game.Board)), "duplicate move changed board");
                        Validate(after, game.Winner, game.IsDraw);
                        Check(Cells(isolated.Board).All(x => x == 0), "cross-session board mutation");
                        moves++;
                    }
                    Check(game.Phase == LocalPhase.MatchOver, "round stuck after nine legal moves");
                    Check(game.AscendantWins + game.VanguardWins + game.Draws == round + 1, "score did not count each round once");
                    var terminal = Cells(game.Board);
                    await game.TryHumanMoveAsync(2, 2);
                    Check(terminal.SequenceEqual(Cells(game.Board)), "terminal board accepted input");
                    await game.PlayAgainAsync();
                    Check(Cells(game.Board).All(x => x == 0) && game.Winner is null && !game.IsDraw, "replay retained stale state");
                }
                game.ResetScores();
                Check(game.AscendantWins + game.VanguardWins + game.Draws == 0, "score reset failed");
            }
            Check(watch.Elapsed < TimeSpan.FromSeconds(10), "local matrix exceeded 10 seconds");
            output.WriteLine($"PASS seed={seed}, 8 hot-seat rounds, random+tactical, invalid/duplicate/menu/replay/reset/isolation; elapsed={watch.ElapsedMilliseconds}ms");
        }
        catch (Exception ex) { throw new Exception($"seed={seed}\n{string.Join("\n", trace)}", ex); }
    }

    [Theory]
    [InlineData(13)]
    [InlineData(2026)]
    [InlineData(104729)]
    public async Task Seeded_players_cannot_beat_Warlord_from_either_seat(int seed)
    {
        var trace = new List<string>();
        var watch = Stopwatch.StartNew();
        try
        {
            foreach (var side in new[] { Player.Ascendant, Player.Vanguard })
            {
                var game = new LocalGameSession();
                var rng = new Random(seed);
                await game.StartVersusComputerAsync(side, Difficulty.Warlord);
                for (var turn = 0; game.Phase == LocalPhase.InMatch && turn < 5; turn++)
                {
                    var before = Cells(game.Board);
                    var move = Choose(before, (int)side, rng, seed != 13);
                    trace.Add($"Warlord human={side} move={move} board={string.Join("", before)}");
                    await game.TryHumanMoveAsync(move / 3, move % 3);
                    var after = Cells(game.Board);
                    Check(before.Select((v, i) => v == 0 || v == after[i]).All(x => x), "AI overwrote an occupied cell");
                    Validate(after, game.Winner, game.IsDraw);
                }
                Check(game.Phase == LocalPhase.MatchOver, "AI round did not terminate");
                Check(game.Winner != side, "Warlord lost to the playtester");
                Check(game.AscendantWins + game.VanguardWins + game.Draws == 1, "AI round score invalid");
                Check(!game.AiThinking, "AI thinking flag stuck after result");
            }
            Check(watch.Elapsed < TimeSpan.FromSeconds(30), "AI matrix exceeded 30 seconds including deliberation");
            output.WriteLine($"PASS seed={seed}, Warlord both seats, legal immutable moves and terminal scores; elapsed={watch.ElapsedMilliseconds}ms");
        }
        catch (Exception ex) { throw new Exception($"seed={seed}\n{string.Join("\n", trace)}", ex); }
    }

    [Theory]
    [InlineData(13)]
    [InlineData(2026)]
    [InlineData(104729)]
    public async Task Seeded_SignalR_players_reconnect_and_complete_isolated_matches(int seed)
    {
        var trace = new List<string>();
        var watch = Stopwatch.StartNew();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<PresenceTracker>();
        builder.Services.AddSingleton<MatchRegistry>();
        builder.Services.AddSingleton<ChallengeBroker>();
        await using var app = builder.Build();
        app.MapHub<GameHub>("/gamehub");
        await app.StartAsync();
        var endpoint = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single() + "/gamehub";
        var registry = app.Services.GetRequiredService<MatchRegistry>();
        var clients = new List<BotClient>();
        async Task<BotClient> Join() { var c = await BotClient.Join(endpoint); clients.Add(c); return c; }
        try
        {
            var outsider = await Join();
            foreach (var tactical in new[] { false, true })
            {
                var a = await Join();
                var b = await Join();
                await a.Hub.InvokeAsync("ChallengePlayer", b.Id);
                var incoming = await b.Next("OnIncomingChallenge");
                await b.Hub.InvokeAsync("RespondToChallenge", incoming.GetProperty("challengeId").GetString(), true);
                var accepted = await a.Next("OnChallengeAccepted");
                await b.Next("OnChallengeAccepted");
                var matchId = accepted.GetProperty("matchId").GetString()!;
                var token = accepted.GetProperty("reconnectToken").GetString()!;
                await a.Next("OnMatchState"); await b.Next("OnMatchState");
                var rng = new Random(seed);
                var board = new int[9];
                for (var ply = 0; ply < 9 && Winner(board) == 0 && board.Contains(0); ply++)
                {
                    if (ply == 2)
                    {
                        var oldId = a.Id;
                        await a.Hub.StopAsync();
                        await b.Next("OnOpponentDisconnected");
                        a = await Join();
                        Check(await a.Hub.InvokeAsync<bool>("RejoinMatch", matchId, token), "rejoin failed");
                        var restored = await a.Next("OnMatchState"); await b.Next("OnMatchState");
                        Check(board.SequenceEqual(WireBoard(restored)), "rejoin changed board");
                        Check(!registry.TryPlace(matchId, oldId, 2, 2).Accepted, "old connection retained move authority");
                        trace.Add("reconnect Ascendant after ply=2; board preserved");
                    }
                    var actor = ply % 2 == 0 ? a : b;
                    var other = ply % 2 == 0 ? b : a;
                    await outsider.Hub.InvokeAsync("ForfeitMatch", matchId);
                    await other.Hub.InvokeAsync("PlaceMove", matchId, 2, 2);
                    await actor.Hub.InvokeAsync("PlaceMove", matchId, -1, 0);
                    Check(board.SequenceEqual(Cells(registry.Get(matchId)!.Board)), "invalid/outsider input changed live match");
                    var move = Choose(board, ply % 2 + 1, rng, tactical);
                    trace.Add($"network profile={(tactical ? "tactical" : "random")} ply={ply} move={move} board={string.Join("", board)}");
                    await actor.Hub.InvokeAsync("PlaceMove", matchId, move / 3, move % 3);
                    var state = await a.Next("OnMatchState"); var otherState = await b.Next("OnMatchState");
                    var next = WireBoard(state);
                    Check(next.SequenceEqual(WireBoard(otherState)), "players diverged");
                    Check(board.Zip(next).Count(pair => pair.First != pair.Second) == 1, "network move changed multiple cells");
                    board = next;
                    await actor.Hub.InvokeAsync("PlaceMove", matchId, move / 3, move % 3);
                }
                Check(Winner(board) != 0 || !board.Contains(0), "network round stuck");
                var endA = await a.Next("OnMatchEnded"); var endB = await b.Next("OnMatchEnded");
                var winning = Winner(board);
                Check(endA.GetProperty("result").GetString() == (winning == 0 ? "draw" : winning == 1 ? "win" : "loss"), "Ascendant result disagrees with board");
                Check(endB.GetProperty("result").GetString() == (winning == 0 ? "draw" : winning == 2 ? "win" : "loss"), "Vanguard result disagrees with board");
                Check(registry.Get(matchId) is null, "finished match leaked");
                Check(!outsider.Has("OnMatchState") && !outsider.Has("OnMatchEnded"), "match leaked to spectator");
            }
            Check(watch.Elapsed < TimeSpan.FromSeconds(60), "network matrix exceeded 60 seconds");
            output.WriteLine($"PASS seed={seed}, 2 real SignalR matches, reconnect/random/tactical/outsider/wrong-turn/invalid/duplicate/isolation; elapsed={watch.ElapsedMilliseconds}ms");
        }
        catch (Exception ex) { throw new Exception($"seed={seed}\n{string.Join("\n", trace)}", ex); }
        finally { foreach (var c in clients) await c.Hub.DisposeAsync(); await app.StopAsync(); }
    }

    private static int[] Cells(Board b) => Enumerable.Range(0, 9).Select(i => (int)b.At(i / 3, i % 3)).ToArray();
    private static int[] WireBoard(JsonElement s) => s.GetProperty("board").EnumerateArray().SelectMany(r => r.EnumerateArray().Select(v => v.GetInt32())).ToArray();
    private static int Winner(int[] b) => Lines.Where(l => b[l[0]] != 0 && b[l[0]] == b[l[1]] && b[l[1]] == b[l[2]]).Select(l => b[l[0]]).FirstOrDefault();
    private static int Choose(int[] b, int side, Random rng, bool tactical)
    {
        var legal = Enumerable.Range(0, 9).Where(i => b[i] == 0).ToArray();
        if (tactical)
        {
            foreach (var mark in new[] { side, 3 - side })
                foreach (var move in legal) { var copy = (int[])b.Clone(); copy[move] = mark; if (Winner(copy) == mark) return move; }
            if (b[4] == 0) return 4;
        }
        return legal[rng.Next(legal.Length)];
    }
    private static void Validate(int[] b, Player? winner, bool draw)
    {
        var a = b.Count(v => v == 1); var v = b.Count(v => v == 2);
        Check(a == v || a == v + 1, "illegal turn count: " + string.Join("", b));
        Check(Winner(b) == (int)(winner ?? Player.None), "winner disagrees with independent line oracle");
        Check(draw == (Winner(b) == 0 && !b.Contains(0)), "draw disagrees with board");
    }
    private static void Check(bool condition, string message) => Assert.True(condition, message);

    private sealed class BotClient
    {
        private readonly Dictionary<string, Channel<JsonElement>> messages = new();
        public HubConnection Hub { get; }
        public string Id { get; private set; } = "";
        private BotClient(string url)
        {
            Hub = new HubConnectionBuilder().WithUrl(url).Build();
            foreach (var name in new[] { "OnLobbyState", "OnIncomingChallenge", "OnChallengeAccepted", "OnMatchState", "OnMatchEnded", "OnOpponentDisconnected" })
            {
                var channel = Channel.CreateUnbounded<JsonElement>(); messages[name] = channel;
                Hub.On<JsonElement>(name, value => channel.Writer.TryWrite(value));
            }
        }
        public static async Task<BotClient> Join(string url)
        {
            var c = new BotClient(url); await c.Hub.StartAsync(); await c.Hub.InvokeAsync("EnterLobby");
            c.Id = (await c.Next("OnLobbyState")).GetProperty("yourConnId").GetString()!; return c;
        }
        public async Task<JsonElement> Next(string name)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await messages[name].Reader.ReadAsync(timeout.Token);
        }
        public bool Has(string name) => messages[name].Reader.TryPeek(out _);
    }
}
