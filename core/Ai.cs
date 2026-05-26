using System;
using System.Collections.Generic;

namespace TicTacToe.Core;

public enum Difficulty
{
    Recruit,
    Captain,
    Warlord
}

public static class Ai
{
    private static readonly Random Rng = CreateRng();

    private static Random CreateRng()
    {
        var seedStr = Environment.GetEnvironmentVariable("TICTACTOE_SEED");
        return int.TryParse(seedStr, out int seed) ? new Random(seed) : new Random();
    }

    public static (int row, int col) ChooseMove(Board board, Player self, Difficulty difficulty)
    {
        return difficulty switch
        {
            Difficulty.Recruit => ChooseRandom(board),
            Difficulty.Captain => ChooseCaptain(board, self),
            Difficulty.Warlord => ChooseWarlord(board, self),
            _                  => ChooseRandom(board)
        };
    }

    private static (int row, int col) ChooseRandom(Board board)
    {
        var cells = new List<(int row, int col)>();
        foreach (var c in board.EmptyCells()) cells.Add(c);
        return cells[Rng.Next(cells.Count)];
    }

    private static (int row, int col) ChooseCaptain(Board board, Player self)
    {
        if (TryFindWinningMove(board, self, out var winning)) return winning;
        if (TryFindWinningMove(board, Board.Other(self), out var blocking)) return blocking;
        return ChooseRandom(board);
    }

    private static bool TryFindWinningMove(Board board, Player p, out (int row, int col) move)
    {
        foreach (var (r, c) in board.EmptyCells())
        {
            var probe = board.Clone();
            probe.TryPlace(r, c, p);
            if (probe.Winner() == p)
            {
                move = (r, c);
                return true;
            }
        }
        move = default;
        return false;
    }

    private static (int row, int col) ChooseWarlord(Board board, Player self)
    {
        int bestScore = int.MinValue;
        (int row, int col) bestMove = (-1, -1);

        foreach (var (r, c) in board.EmptyCells())
        {
            var probe = board.Clone();
            probe.TryPlace(r, c, self);
            int score = Minimax(probe, Board.Other(self), self, depth: 1);
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = (r, c);
            }
        }

        return bestMove.row >= 0 ? bestMove : ChooseRandom(board);
    }

    // Minimax score from `self`'s perspective:
    //   +(10-depth) for self win, -(10-depth) for opponent win, 0 for draw.
    // Subtracting depth makes the AI prefer faster wins and slower losses.
    private static int Minimax(Board board, Player toMove, Player self, int depth)
    {
        var winner = board.Winner();
        if (winner == self)              return 10 - depth;
        if (winner == Board.Other(self)) return depth - 10;
        if (board.IsDraw())              return 0;

        bool maximizing = toMove == self;
        int best = maximizing ? int.MinValue : int.MaxValue;

        foreach (var (r, c) in board.EmptyCells())
        {
            var probe = board.Clone();
            probe.TryPlace(r, c, toMove);
            int score = Minimax(probe, Board.Other(toMove), self, depth + 1);
            best = maximizing ? Math.Max(best, score) : Math.Min(best, score);
        }
        return best;
    }
}
