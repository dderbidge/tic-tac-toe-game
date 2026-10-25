using Microsoft.JSInterop;
using TicTacToe.Core;
using TicTacToe.Web.Services;
using TicTacToe.WebMultiplayer.Services;
using Xunit;

namespace TicTacToe.WebMultiplayer.Tests;

public sealed class LocalGameSessionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Leaving_during_an_ai_turn_does_not_change_the_hidden_board(bool wasm)
    {
        var game = Create(wasm);
        var pending = game.StartAi(Player.Vanguard, Difficulty.Warlord);
        game.Menu();
        await pending;

        Assert.Equal("MainMenu", game.Phase());
        Assert.Equal(9, game.Board().EmptyCells().Count());
        Assert.False(game.Thinking());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_pending_ai_turn_cannot_play_on_a_replacement_hot_seat_board(bool wasm)
    {
        var game = Create(wasm);
        var pending = game.StartAi(Player.Vanguard, Difficulty.Warlord);
        game.HotSeat();
        await pending;

        Assert.Equal("InMatch", game.Phase());
        Assert.Equal(9, game.Board().EmptyCells().Count());
        Assert.Equal(Player.Ascendant, game.Active());
        Assert.False(game.Thinking());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Starting_ai_twice_only_allows_the_current_match_to_make_one_opening_move(bool wasm)
    {
        var game = Create(wasm);
        var oldTurn = game.StartAi(Player.Vanguard, Difficulty.Warlord);
        var currentTurn = game.StartAi(Player.Vanguard, Difficulty.Warlord);
        await Task.WhenAll(oldTurn, currentTurn);

        Assert.Equal(8, game.Board().EmptyCells().Count());
        Assert.Equal(Player.Vanguard, game.Active());
        Assert.False(game.Thinking());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalid_duplicate_and_post_win_moves_do_not_change_turn_or_score(bool wasm)
    {
        var game = Create(wasm);
        game.HotSeat();
        await game.Move(-1, 0);
        await game.Move(3, 0);
        Assert.Equal(Player.Ascendant, game.Active());
        await game.Move(0, 0);
        await game.Move(0, 0);
        Assert.Equal(Player.Vanguard, game.Active());
        Assert.Equal(8, game.Board().EmptyCells().Count());
        await game.Move(1, 0);
        await game.Move(0, 1);
        await game.Move(1, 1);
        await game.Move(0, 2);
        await game.Move(2, 2);

        Assert.Equal("MatchOver", game.Phase());
        Assert.Equal((1, 0, 0), game.Scores());
        Assert.Equal(Player.None, game.Board().At(2, 2));
        await game.Replay();
        Assert.Equal("InMatch", game.Phase());
        Assert.Equal(9, game.Board().EmptyCells().Count());
        Assert.Equal((1, 0, 0), game.Scores());
        await game.Reset();
        Assert.Equal((0, 0, 0), game.Scores());
    }

    private static Session Create(bool wasm)
    {
        if (wasm)
        {
            var g = new GameSession(new MemoryJs());
            return new(g.StartVersusComputerAsync, g.StartHotSeat, g.GoToMainMenu,
                g.TryHumanMoveAsync, g.PlayAgainAsync, g.ResetScoresAsync,
                () => g.Board, () => g.Active, () => g.Phase.ToString(), () => g.AiThinking,
                () => (g.AscendantWins, g.VanguardWins, g.Draws));
        }
        var local = new LocalGameSession();
        return new(local.StartVersusComputerAsync, local.StartHotSeat, local.GoToMainMenu,
            local.TryHumanMoveAsync, local.PlayAgainAsync, () => { local.ResetScores(); return Task.CompletedTask; },
            () => local.Board, () => local.Active, () => local.Phase.ToString(), () => local.AiThinking,
            () => (local.AscendantWins, local.VanguardWins, local.Draws));
    }

    private sealed record Session(
        Func<Player, Difficulty, Task> StartAi, Action HotSeat, Action Menu,
        Func<int, int, Task> Move, Func<Task> Replay, Func<Task> Reset,
        Func<Board> Board, Func<Player> Active, Func<string> Phase, Func<bool> Thinking,
        Func<(int, int, int)> Scores);

    private sealed class MemoryJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken token, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }
}
