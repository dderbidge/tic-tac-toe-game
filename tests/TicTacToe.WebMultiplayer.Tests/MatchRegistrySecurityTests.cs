using System.Text.Json;
using TicTacToe.Core;
using TicTacToe.WebMultiplayer.Services;
using Xunit;

namespace TicTacToe.WebMultiplayer.Tests;

public sealed class MatchRegistrySecurityTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);

    [Fact]
    public void New_matches_issue_distinct_256_bit_tokens_and_never_serialize_them()
    {
        var registry = new MatchRegistry();
        var tokens = new HashSet<string>();
        for (var i = 0; i < 16; i++)
        {
            var match = registry.Create($"asc-{i}", "Ascendant", $"van-{i}", "Vanguard");
            Assert.Equal(32, Convert.FromHexString(match.AscendantReconnectToken!).Length);
            Assert.Equal(32, Convert.FromHexString(match.VanguardReconnectToken!).Length);
            Assert.True(tokens.Add(match.AscendantReconnectToken!));
            Assert.True(tokens.Add(match.VanguardReconnectToken!));
            var serialized = JsonSerializer.Serialize(match);
            Assert.DoesNotContain("ReconnectToken", serialized);
            Assert.DoesNotContain(match.AscendantReconnectToken!, serialized);
            Assert.DoesNotContain(match.VanguardReconnectToken!, serialized);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Valid_token_restores_its_own_faction_board_and_turn_and_revokes_old_connection(bool ascendant)
    {
        var clock = new ManualTimeProvider();
        var registry = new MatchRegistry(clock);
        var match = Create(registry);
        Assert.True(registry.TryPlace(match.MatchId, "asc", 0, 0).Accepted);
        var oldConnection = ascendant ? "asc" : "van";
        var token = ascendant ? match.AscendantReconnectToken! : match.VanguardReconnectToken!;
        Assert.Same(match, registry.MarkDisconnected(match.MatchId, oldConnection, Grace, (_, _, _) => { }));
        var timer = clock.LastTimer;

        Assert.Same(match, registry.TryRejoin(match.MatchId, "replacement", token));
        Assert.Equal(ascendant ? "replacement" : "asc", match.AscendantConnId);
        Assert.Equal(ascendant ? "van" : "replacement", match.VanguardConnId);
        Assert.Equal("Original Ascendant", match.AscendantName);
        Assert.Equal("Original Vanguard", match.VanguardName);
        Assert.Equal(Player.Ascendant, match.Board.At(0, 0));
        Assert.Equal(Player.Vanguard, match.Active);
        Assert.Equal(token, ascendant ? match.AscendantReconnectToken : match.VanguardReconnectToken);
        Assert.True(timer.IsDisposed);
        Assert.False(registry.TryPlace(match.MatchId, oldConnection, 1, 1).Accepted);
        Assert.Null(registry.Forfeit(match.MatchId, oldConnection));
        Assert.True(registry.TryPlace(match.MatchId, ascendant ? "van" : "replacement", 1, 1).Accepted);
        Assert.Same(match, registry.TryRejoin(match.MatchId, "replacement", token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-a-token")]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    public void Missing_or_incorrect_tokens_cannot_claim_a_disconnected_seat(string? badToken)
    {
        var registry = new MatchRegistry(new ManualTimeProvider());
        var match = Create(registry);
        var token = match.AscendantReconnectToken!;
        registry.MarkDisconnected(match.MatchId, "asc", Grace, (_, _, _) => { });

        Assert.Null(registry.TryRejoin(match.MatchId, "attacker", badToken));
        Assert.Equal("asc", match.AscendantConnId);
        Assert.Null(match.Winner);
        Assert.Same(match, registry.TryRejoin(match.MatchId, "legitimate", token));
    }

    [Fact]
    public void Token_cannot_take_a_connected_seat_or_both_seats_or_a_seat_in_a_second_match()
    {
        var registry = new MatchRegistry(new ManualTimeProvider());
        var match = Create(registry);
        Assert.Null(registry.TryRejoin(match.MatchId, "attacker", match.AscendantReconnectToken));
        registry.MarkDisconnected(match.MatchId, "asc", Grace, (_, _, _) => { });
        Assert.Null(registry.TryRejoin(match.MatchId, "attacker", match.VanguardReconnectToken));
        Assert.Null(registry.TryRejoin(match.MatchId, "van", match.AscendantReconnectToken));
        registry.Create("other-asc", "Other A", "other-van", "Other V");
        Assert.Null(registry.TryRejoin(match.MatchId, "other-asc", match.AscendantReconnectToken));
        Assert.Same(match, registry.TryRejoin(match.MatchId, "legitimate", match.AscendantReconnectToken));
    }

    [Fact]
    public void A_different_match_token_cannot_claim_a_seat()
    {
        var registry = new MatchRegistry(new ManualTimeProvider());
        var first = Create(registry);
        var second = registry.Create("other-asc", "Other A", "other-van", "Other V");
        registry.MarkDisconnected(first.MatchId, "asc", Grace, (_, _, _) => { });
        registry.MarkDisconnected(second.MatchId, "other-asc", Grace, (_, _, _) => { });
        Assert.Null(registry.TryRejoin(first.MatchId, "attacker", second.AscendantReconnectToken));
        Assert.Equal("asc", first.AscendantConnId);
        Assert.Equal("other-asc", second.AscendantConnId);
    }

    [Fact]
    public void Outsider_cannot_create_a_disconnect_lease_or_block_the_actual_participant()
    {
        var clock = new ManualTimeProvider();
        var registry = new MatchRegistry(clock);
        var match = Create(registry);
        Assert.Null(registry.MarkDisconnected(match.MatchId, "outsider", Grace, (_, _, _) => { }));
        Assert.Null(registry.MarkDisconnected("missing", "asc", Grace, (_, _, _) => { }));
        Assert.True(registry.TryPlace(match.MatchId, "asc", 0, 0).Accepted);
        Assert.Same(match, registry.MarkDisconnected(match.MatchId, "van", Grace, (_, _, _) => { }));
        Assert.Same(match, registry.TryRejoin(match.MatchId, "van-new", match.VanguardReconnectToken));
        Assert.True(registry.TryPlace(match.MatchId, "van-new", 1, 1).Accepted);
    }

    [Fact]
    public void Both_players_can_disconnect_and_rejoin_independently_with_their_own_tokens()
    {
        var clock = new ManualTimeProvider();
        var registry = new MatchRegistry(clock);
        var match = Create(registry);
        var ascToken = match.AscendantReconnectToken;
        var vanToken = match.VanguardReconnectToken;
        registry.MarkDisconnected(match.MatchId, "asc", Grace, (_, _, _) => { });
        var ascTimer = clock.LastTimer;
        registry.MarkDisconnected(match.MatchId, "van", Grace, (_, _, _) => { });
        var vanTimer = clock.LastTimer;

        Assert.Same(match, registry.TryRejoin(match.MatchId, "van-new", vanToken));
        Assert.True(vanTimer.IsDisposed);
        Assert.False(ascTimer.IsDisposed);
        Assert.Same(match, registry.TryRejoin(match.MatchId, "asc-new", ascToken));
        Assert.True(ascTimer.IsDisposed);
        Assert.Equal("asc-new", match.AscendantConnId);
        Assert.Equal("van-new", match.VanguardConnId);
        Assert.Null(match.Winner);
    }

    [Theory]
    [InlineData("asc", Player.Vanguard)]
    [InlineData("van", Player.Ascendant)]
    public void Only_a_participant_can_forfeit_and_success_revokes_both_credentials(string participant, Player winner)
    {
        var clock = new ManualTimeProvider();
        var registry = new MatchRegistry(clock);
        var match = Create(registry);
        var token = match.AscendantReconnectToken;
        registry.MarkDisconnected(match.MatchId, "asc", Grace, (_, _, _) => { });
        var timer = clock.LastTimer;

        Assert.Null(registry.Forfeit(match.MatchId, "outsider"));
        Assert.Null(match.Winner);
        Assert.Equal(token, match.AscendantReconnectToken);
        Assert.False(timer.IsDisposed);
        Assert.Same(match, registry.Forfeit(match.MatchId, participant));
        Assert.Equal(winner, match.Winner);
        Assert.Null(match.AscendantReconnectToken);
        Assert.Null(match.VanguardReconnectToken);
        Assert.True(timer.IsDisposed);
        Assert.Null(registry.Forfeit(match.MatchId, "outsider"));
        Assert.Null(registry.Forfeit(match.MatchId, participant));
        Assert.Null(registry.TryRejoin(match.MatchId, "replacement", token));
        Assert.Null(registry.Forfeit("missing-match", participant));
    }

    [Fact]
    public void Duplicate_disconnect_does_not_extend_grace_and_expiry_is_checked_before_callback_dispatch()
    {
        var clock = new ManualTimeProvider();
        var registry = new MatchRegistry(clock);
        var match = Create(registry);
        var token = match.AscendantReconnectToken;
        var lease = Disconnect(registry, clock, match.MatchId, "asc");
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.Null(registry.MarkDisconnected(match.MatchId, "asc", Grace, (_, _, _) => { }));
        Assert.Null(registry.ForfeitDisconnected(match.MatchId, "asc", lease));
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Null(registry.TryRejoin(match.MatchId, "replacement", token));
        Assert.Same(match, registry.ForfeitDisconnected(match.MatchId, "asc", lease));
        Assert.Equal(Player.Vanguard, match.Winner);
        Assert.Null(match.AscendantReconnectToken);
        Assert.Null(match.VanguardReconnectToken);
    }

    [Fact]
    public void Queued_callback_cannot_forfeit_a_rejoined_player_or_a_new_disconnect_generation()
    {
        var clock = new ManualTimeProvider();
        var registry = new MatchRegistry(clock);
        var match = Create(registry);
        var token = match.AscendantReconnectToken;
        var staleLease = Disconnect(registry, clock, match.MatchId, "asc");
        Assert.Same(match, registry.TryRejoin(match.MatchId, "asc", token));
        var currentLease = Disconnect(registry, clock, match.MatchId, "asc");
        Assert.NotEqual(staleLease, currentLease);
        clock.Advance(Grace);
        Assert.Null(registry.ForfeitDisconnected(match.MatchId, "asc", staleLease));
        Assert.Null(registry.ForfeitDisconnected(match.MatchId, "van", currentLease));
        Assert.Null(match.Winner);
        Assert.Same(match, registry.ForfeitDisconnected(match.MatchId, "asc", currentLease));
        Assert.Null(registry.ForfeitDisconnected(match.MatchId, "asc", currentLease));
    }

    [Fact]
    public void Removing_match_disposes_timers_and_revokes_all_saved_tokens()
    {
        var clock = new ManualTimeProvider();
        var registry = new MatchRegistry(clock);
        var match = Create(registry);
        var token = match.AscendantReconnectToken;
        var lease = Disconnect(registry, clock, match.MatchId, "asc");
        var timer = clock.LastTimer;
        Assert.True(registry.Remove(match.MatchId));
        Assert.True(timer.IsDisposed);
        Assert.Null(match.AscendantReconnectToken);
        Assert.Null(match.VanguardReconnectToken);
        Assert.Null(registry.TryRejoin(match.MatchId, "replacement", token));
        clock.Advance(Grace);
        Assert.Null(registry.ForfeitDisconnected(match.MatchId, "asc", lease));
        Assert.False(registry.Remove(match.MatchId));
    }

    [Fact]
    public void Disposing_registry_clears_every_match_token_and_pending_timer()
    {
        var clock = new ManualTimeProvider();
        var registry = new MatchRegistry(clock);
        var first = Create(registry);
        var second = registry.Create("other-asc", "Other A", "other-van", "Other V");
        var token = first.AscendantReconnectToken;
        var lease = Disconnect(registry, clock, first.MatchId, "asc");
        var firstAscTimer = clock.LastTimer;
        registry.MarkDisconnected(first.MatchId, "van", Grace, (_, _, _) => { });
        var firstVanTimer = clock.LastTimer;
        registry.MarkDisconnected(second.MatchId, "other-asc", Grace, (_, _, _) => { });
        var secondTimer = clock.LastTimer;

        registry.Dispose();
        registry.Dispose();
        Assert.True(firstAscTimer.IsDisposed);
        Assert.True(firstVanTimer.IsDisposed);
        Assert.True(secondTimer.IsDisposed);
        Assert.Null(first.AscendantReconnectToken);
        Assert.Null(first.VanguardReconnectToken);
        Assert.Null(second.AscendantReconnectToken);
        Assert.Null(second.VanguardReconnectToken);
        Assert.Null(registry.Get(first.MatchId));
        Assert.Null(registry.Get(second.MatchId));
        Assert.Null(registry.TryRejoin(first.MatchId, "replacement", token));
        clock.Advance(Grace);
        Assert.Null(registry.ForfeitDisconnected(first.MatchId, "asc", lease));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Winning_move_or_draw_revokes_tokens_and_pending_disconnect_timers(bool draw)
    {
        var clock = new ManualTimeProvider();
        var registry = new MatchRegistry(clock);
        var match = Create(registry);
        var moves = draw
            ? new[] { (0, 0), (0, 1), (0, 2), (1, 1), (1, 0), (1, 2), (2, 1), (2, 0), (2, 2) }
            : new[] { (0, 0), (1, 0), (0, 1), (1, 1), (0, 2) };
        for (var i = 0; i < moves.Length - 1; i++)
            Assert.True(registry.TryPlace(match.MatchId, i % 2 == 0 ? "asc" : "van", moves[i].Item1, moves[i].Item2).Accepted);
        var lease = Disconnect(registry, clock, match.MatchId, "van");
        var timer = clock.LastTimer;
        var last = moves[^1];
        Assert.True(registry.TryPlace(match.MatchId, "asc", last.Item1, last.Item2).Accepted);
        Assert.Equal(draw, match.IsDraw);
        Assert.Equal<Player?>(draw ? null : Player.Ascendant, match.Winner);
        Assert.Null(match.AscendantReconnectToken);
        Assert.Null(match.VanguardReconnectToken);
        Assert.True(timer.IsDisposed);
        clock.Advance(Grace);
        Assert.Null(registry.ForfeitDisconnected(match.MatchId, "van", lease));
    }

    [Fact]
    public async Task Simultaneous_rejoins_can_claim_each_seat_only_once()
    {
        var registry = new MatchRegistry(new ManualTimeProvider());
        var match = Create(registry);
        var token = match.AscendantReconnectToken;
        registry.MarkDisconnected(match.MatchId, "asc", Grace, (_, _, _) => { });
        using var start = new ManualResetEventSlim();
        var attempts = Enumerable.Range(0, 16).Select(i => Task.Run(() =>
        {
            start.Wait();
            return registry.TryRejoin(match.MatchId, $"replacement-{i}", token);
        })).ToArray();
        start.Set();
        var results = await Task.WhenAll(attempts);
        Assert.Single(results, result => result is not null);
        Assert.StartsWith("replacement-", match.AscendantConnId);
        Assert.Null(match.Winner);
    }

    [Fact]
    public async Task Rejoin_and_grace_expiry_have_only_one_successful_outcome()
    {
        for (var iteration = 0; iteration < 100; iteration++)
        {
            var clock = new ManualTimeProvider();
            var registry = new MatchRegistry(clock);
            var match = Create(registry);
            var token = match.AscendantReconnectToken;
            var lease = Disconnect(registry, clock, match.MatchId, "asc");
            using var start = new ManualResetEventSlim();
            var rejoin = Task.Run(() => { start.Wait(); return registry.TryRejoin(match.MatchId, "replacement", token); });
            var expiry = Task.Run(() =>
            {
                start.Wait();
                clock.Advance(Grace);
                return registry.ForfeitDisconnected(match.MatchId, "asc", lease);
            });
            start.Set();
            var results = await Task.WhenAll(rejoin, expiry);
            Assert.NotEqual(results[0] is null, results[1] is null);
            if (results[0] is not null)
            {
                Assert.Equal("replacement", match.AscendantConnId);
                Assert.Null(match.Winner);
                Assert.Equal(token, match.AscendantReconnectToken);
            }
            else
            {
                Assert.Equal("asc", match.AscendantConnId);
                Assert.Equal(Player.Vanguard, match.Winner);
                Assert.Null(match.AscendantReconnectToken);
            }
        }
    }

    [Fact]
    public async Task Rejoin_and_a_queued_old_connection_forfeit_cannot_both_succeed()
    {
        for (var iteration = 0; iteration < 50; iteration++)
        {
            var registry = new MatchRegistry(new ManualTimeProvider());
            var match = Create(registry);
            var token = match.AscendantReconnectToken;
            registry.MarkDisconnected(match.MatchId, "asc", Grace, (_, _, _) => { });
            using var start = new ManualResetEventSlim();
            var rejoin = Task.Run(() => { start.Wait(); return registry.TryRejoin(match.MatchId, "replacement", token); });
            var forfeit = Task.Run(() => { start.Wait(); return registry.Forfeit(match.MatchId, "asc"); });
            start.Set();
            var results = await Task.WhenAll(rejoin, forfeit);
            Assert.NotEqual(results[0] is null, results[1] is null);
            if (results[0] is not null)
            {
                Assert.Equal("replacement", match.AscendantConnId);
                Assert.Null(match.Winner);
                Assert.Null(registry.Forfeit(match.MatchId, "asc"));
            }
            else
            {
                Assert.Equal(Player.Vanguard, match.Winner);
                Assert.Null(match.AscendantReconnectToken);
            }
        }
    }

    private static MultiplayerMatch Create(MatchRegistry registry) =>
        registry.Create("asc", "Original Ascendant", "van", "Original Vanguard");

    private static Guid Disconnect(MatchRegistry registry, ManualTimeProvider clock, string matchId, string connection)
    {
        Guid lease = default;
        Assert.NotNull(registry.MarkDisconnected(matchId, connection, Grace, (_, _, id) => lease = id));
        clock.LastTimer.FireQueuedCallback();
        Assert.NotEqual(Guid.Empty, lease);
        return lease;
    }
}
