using System.Collections.Generic;

namespace TicTacToe.Core;

public enum Player
{
    None,
    Ascendant,
    Vanguard
}

public sealed class Board
{
    private readonly Player[,] _cells = new Player[3, 3];

    public Player At(int row, int col) => _cells[row, col];

    public bool TryPlace(int row, int col, Player p)
    {
        if ((uint)row >= 3 || (uint)col >= 3) return false;
        if (p == Player.None) return false;
        if (_cells[row, col] != Player.None) return false;
        _cells[row, col] = p;
        return true;
    }

    public Player? Winner()
    {
        for (int r = 0; r < 3; r++)
            if (_cells[r, 0] != Player.None &&
                _cells[r, 0] == _cells[r, 1] &&
                _cells[r, 1] == _cells[r, 2])
                return _cells[r, 0];

        for (int c = 0; c < 3; c++)
            if (_cells[0, c] != Player.None &&
                _cells[0, c] == _cells[1, c] &&
                _cells[1, c] == _cells[2, c])
                return _cells[0, c];

        if (_cells[0, 0] != Player.None &&
            _cells[0, 0] == _cells[1, 1] &&
            _cells[1, 1] == _cells[2, 2])
            return _cells[0, 0];

        if (_cells[0, 2] != Player.None &&
            _cells[0, 2] == _cells[1, 1] &&
            _cells[1, 1] == _cells[2, 0])
            return _cells[0, 2];

        return null;
    }

    public bool IsDraw()
    {
        if (Winner() != null) return false;
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                if (_cells[r, c] == Player.None)
                    return false;
        return true;
    }

    public bool IsGameOver() => Winner() != null || IsDraw();

    public IEnumerable<(int row, int col)> EmptyCells()
    {
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                if (_cells[r, c] == Player.None)
                    yield return (r, c);
    }

    public Board Clone()
    {
        var copy = new Board();
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                copy._cells[r, c] = _cells[r, c];
        return copy;
    }

    public static Player Other(Player p) => p switch
    {
        Player.Ascendant => Player.Vanguard,
        Player.Vanguard  => Player.Ascendant,
        _                => Player.None
    };
}
