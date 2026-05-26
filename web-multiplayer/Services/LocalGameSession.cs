using System;
using System.Threading.Tasks;
using TicTacToe.Core;

namespace TicTacToe.WebMultiplayer.Services;

public enum LocalPhase
{
    MainMenu,
    ModeSetup,
    InMatch,
    MatchOver
}

public sealed class LocalGameSession
{
    private const int AiDeliberationMs = 600;

    public LocalPhase Phase { get; private set; } = LocalPhase.MainMenu;
    public Board Board { get; private set; } = new();
    public Player Active { get; private set; } = Player.Ascendant;
    public bool VersusComputer { get; private set; }
    public Player HumanSide { get; private set; } = Player.Ascendant;
    public Difficulty AiDifficulty { get; private set; } = Difficulty.Recruit;

    public int AscendantWins { get; private set; }
    public int VanguardWins  { get; private set; }
    public int Draws         { get; private set; }

    public Player? Winner { get; private set; }
    public bool IsDraw { get; private set; }
    public (int row, int col)[]? WinningCells { get; private set; }
    public bool AiThinking { get; private set; }

    public event Action? OnChange;
    private void NotifyChanged() => OnChange?.Invoke();

    public void StartHotSeat()
    {
        VersusComputer = false;
        BeginMatch();
    }

    public void GoToModeSetup()
    {
        Phase = LocalPhase.ModeSetup;
        NotifyChanged();
    }

    public async Task StartVersusComputerAsync(Player humanSide, Difficulty difficulty)
    {
        VersusComputer = true;
        HumanSide = humanSide;
        AiDifficulty = difficulty;
        BeginMatch();
        if (Active != HumanSide) await PlayAiTurnAsync();
    }

    private void BeginMatch()
    {
        Board = new Board();
        Active = Player.Ascendant;
        Winner = null;
        IsDraw = false;
        WinningCells = null;
        Phase = LocalPhase.InMatch;
        NotifyChanged();
    }

    public void GoToMainMenu()
    {
        Phase = LocalPhase.MainMenu;
        NotifyChanged();
    }

    public async Task PlayAgainAsync()
    {
        BeginMatch();
        if (VersusComputer && Active != HumanSide) await PlayAiTurnAsync();
    }

    public async Task TryHumanMoveAsync(int row, int col)
    {
        if (Phase != LocalPhase.InMatch) return;
        if (VersusComputer && Active != HumanSide) return;
        if (!Board.TryPlace(row, col, Active)) return;

        if (CheckEnd()) return;
        Active = Board.Other(Active);
        NotifyChanged();

        if (VersusComputer) await PlayAiTurnAsync();
    }

    private async Task PlayAiTurnAsync()
    {
        if (Phase != LocalPhase.InMatch) return;
        AiThinking = true;
        NotifyChanged();
        await Task.Delay(AiDeliberationMs);

        var (row, col) = Ai.ChooseMove(Board, Active, AiDifficulty);
        Board.TryPlace(row, col, Active);
        AiThinking = false;

        if (CheckEnd()) return;
        Active = Board.Other(Active);
        NotifyChanged();
    }

    public void ResetScores()
    {
        AscendantWins = VanguardWins = Draws = 0;
        NotifyChanged();
    }

    private bool CheckEnd()
    {
        var winner = Board.Winner();
        if (winner is { } w)
        {
            Winner = w;
            WinningCells = ComputeWinningCells();
            if (w == Player.Ascendant) AscendantWins++; else VanguardWins++;
            Phase = LocalPhase.MatchOver;
            NotifyChanged();
            return true;
        }
        if (Board.IsDraw())
        {
            IsDraw = true;
            Draws++;
            Phase = LocalPhase.MatchOver;
            NotifyChanged();
            return true;
        }
        return false;
    }

    private (int row, int col)[]? ComputeWinningCells()
    {
        for (int r = 0; r < 3; r++)
            if (Board.At(r, 0) != Player.None && Board.At(r, 0) == Board.At(r, 1) && Board.At(r, 1) == Board.At(r, 2))
                return new[] { (r, 0), (r, 1), (r, 2) };
        for (int c = 0; c < 3; c++)
            if (Board.At(0, c) != Player.None && Board.At(0, c) == Board.At(1, c) && Board.At(1, c) == Board.At(2, c))
                return new[] { (0, c), (1, c), (2, c) };
        if (Board.At(0, 0) != Player.None && Board.At(0, 0) == Board.At(1, 1) && Board.At(1, 1) == Board.At(2, 2))
            return new[] { (0, 0), (1, 1), (2, 2) };
        if (Board.At(0, 2) != Player.None && Board.At(0, 2) == Board.At(1, 1) && Board.At(1, 1) == Board.At(2, 0))
            return new[] { (0, 2), (1, 1), (2, 0) };
        return null;
    }
}
