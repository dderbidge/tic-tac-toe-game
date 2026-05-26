using System;
using System.IO;
using TicTacToe.Core;

namespace TicTacToe;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            Renderer.Initialize();
            Renderer.DrawTitle();
            RunMainMenu();
            return 0;
        }
        catch (EndOfStreamException)
        {
            Console.WriteLine();
            Console.WriteLine("  Input stream closed. Goodbye, captain.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("  A subspace fault has interrupted the game.");
            Console.Error.WriteLine($"  {ex.Message}");
            return 1;
        }
    }

    private static void RunMainMenu()
    {
        while (true)
        {
            int choice = Renderer.PromptMenu(
                "Main Menu",
                new[] { "Hot-seat (two humans)", "Versus the computer", "Quit" });

            switch (choice)
            {
                case 0: RunHotSeatSession(); break;
                case 1: RunVersusComputerSession(); break;
                case 2: return;
            }
        }
    }

    private static void RunHotSeatSession()
    {
        int ascWins = 0, vanWins = 0, draws = 0;
        while (true)
        {
            var result = PlayMatch(humanSide: Player.Ascendant, opponentIsAi: false, aiDifficulty: Difficulty.Recruit);
            UpdateScore(result, ref ascWins, ref vanWins, ref draws);
            Renderer.DrawScore(ascWins, vanWins, draws);
            if (!Renderer.PromptYesNo("Play again?")) return;
        }
    }

    private static void RunVersusComputerSession()
    {
        int factionChoice = Renderer.PromptMenu(
            "Choose your faction",
            new[]
            {
                $"{ThemeText.AscendantName} ({ThemeText.Sun}) — moves first",
                $"{ThemeText.VanguardName} ({ThemeText.Sparkle}) — moves second"
            });
        Player human = factionChoice == 0 ? Player.Ascendant : Player.Vanguard;

        int diffChoice = Renderer.PromptMenu(
            "Choose opponent difficulty",
            new[] { "Recruit (random)", "Captain (tactical)", "Warlord (unbeatable)" });
        Difficulty diff = (Difficulty)diffChoice;

        int ascWins = 0, vanWins = 0, draws = 0;
        while (true)
        {
            var result = PlayMatch(humanSide: human, opponentIsAi: true, aiDifficulty: diff);
            UpdateScore(result, ref ascWins, ref vanWins, ref draws);
            Renderer.DrawScore(ascWins, vanWins, draws);
            if (!Renderer.PromptYesNo("Play again?")) return;
        }
    }

    private enum MatchResult { AscendantWins, VanguardWins, Draw }

    private static MatchResult PlayMatch(Player humanSide, bool opponentIsAi, Difficulty aiDifficulty)
    {
        var board = new Board();
        Player active = Player.Ascendant;

        while (true)
        {
            Renderer.DrawBoard(board);

            (int row, int col) move;
            if (opponentIsAi && active != humanSide)
            {
                Renderer.Announce($"{ConsolePalette.ColoredNameOf(active)} ({ThemeText.DifficultyName(aiDifficulty)}) considers the grid…");
                move = Ai.ChooseMove(board, active, aiDifficulty);
            }
            else
            {
                move = Renderer.PromptForCoordinate(active, board);
            }

            board.TryPlace(move.row, move.col, active);
            Renderer.Announce(ConsolePalette.PlacementMessage(active, Renderer.SectorLabel(move.row, move.col)));

            var winner = board.Winner();
            if (winner is { } w)
            {
                Renderer.DrawBoard(board);
                Renderer.Announce(ConsolePalette.WinMessage(w));
                return w == Player.Ascendant ? MatchResult.AscendantWins : MatchResult.VanguardWins;
            }
            if (board.IsDraw())
            {
                Renderer.DrawBoard(board);
                Renderer.Announce(ThemeText.DrawMessage());
                return MatchResult.Draw;
            }

            active = Board.Other(active);
        }
    }

    private static void UpdateScore(MatchResult result, ref int asc, ref int van, ref int draws)
    {
        switch (result)
        {
            case MatchResult.AscendantWins: asc++; break;
            case MatchResult.VanguardWins:  van++; break;
            case MatchResult.Draw:          draws++; break;
        }
    }
}
