# Tic-Tac-Toe — "Order vs Rebellion" Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a complete, runnable C# console tic-tac-toe game with a "light skin" space-opera theme (The Ascendant ☼ vs The Crimson Vanguard ✦), supporting hot-seat and vs-AI play with three difficulty tiers.

**Architecture:** Single .NET 9 console project, five source files with one responsibility each: `Program.cs` (entry/control flow), `Board.cs` (pure game state), `Ai.cs` (move selection by difficulty), `Renderer.cs` (all I/O and ANSI), `Theme.cs` (constants and flavor strings). Match loop is driven by `Program.cs`; everything below it is stateless except `Board`.

**Tech Stack:** .NET SDK 9.0.305, C# 12, no external NuGet packages.

**Note on testing:** The spec deliberately chose structure B (no separate xUnit test project). This plan substitutes runtime-verification steps and a manual smoke checklist for what would normally be unit tests. If a future iteration adds a `TicTacToe.Tests` project, every public method on `Board` and the three `Ai` tiers are designed to be unit-testable as-is.

---

## File Structure

| File | Responsibility |
|---|---|
| `TicTacToe.csproj` | Project metadata, target framework, nullable, encoding |
| `Program.cs` | `Main`, menus, match loop, score tracking |
| `Board.cs` | `Player` enum, `Board` class — state, placement, win/draw detection, cloning |
| `Ai.cs` | `Difficulty` enum, `Ai.ChooseMove(...)` — Recruit/Captain/Warlord |
| `Renderer.cs` | All `Console` I/O, ANSI color, board drawing, input parsing |
| `Theme.cs` | Faction names, symbols, color sequences, flavor message helpers |
| `MORNING.md` | End-of-night summary for the user (final task) |

---

## Task 1: Scaffold the project

**Files:**
- Create: `./TicTacToe.csproj`
- Create: `./Program.cs` (placeholder)

- [ ] **Step 1: Initialize the .NET console project**

Run from `.`:
```powershell
dotnet new console --name TicTacToe --output . --framework net9.0 --force
```
Expected: creates `TicTacToe.csproj` and a top-level-statements `Program.cs`.

- [ ] **Step 2: Replace `Program.cs` with a temporary placeholder using an explicit Main**

This makes the file structure consistent with how `Board.cs`/`Ai.cs` reference `Program` later.

```csharp
using System;
using System.Text;

namespace TicTacToe;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("Tic-tac-toe scaffold OK.");
        return 0;
    }
}
```

- [ ] **Step 3: Verify build and run**

```powershell
dotnet build
dotnet run --project .
```
Expected build: `Build succeeded` with 0 warnings, 0 errors.
Expected run: prints `Tic-tac-toe scaffold OK.`

- [ ] **Step 4: Add `.gitignore` for build artifacts**

Create `./.gitignore`:
```
bin/
obj/
.vs/
*.user
.superpowers/
```

(`.superpowers/` keeps the brainstorming-companion files out of source control if git is added later.)

---

## Task 2: Theme constants and flavor strings

**Files:**
- Create: `./Theme.cs`

- [ ] **Step 1: Write `Theme.cs`**

```csharp
using System;

namespace TicTacToe;

public static class Theme
{
    public const string AscendantName = "The Ascendant";
    public const string VanguardName  = "The Crimson Vanguard";

    public const string AscendantSymbol = "♀"; // placeholder — overridden below
    public const string VanguardSymbol  = "♁"; // placeholder — overridden below

    // Real glyphs:
    public const string Sun     = "☼"; // ☼
    public const string Sparkle = "✦"; // ✦

    // ANSI sequences. UseColor is set at startup by Renderer.
    public static bool UseColor { get; set; } = true;

    public const string AnsiReset    = "[0m";
    public const string AnsiGold     = "[33m";
    public const string AnsiCrimson  = "[31m";
    public const string AnsiDim      = "[2m";
    public const string AnsiBold     = "[1m";

    public static string Color(string ansi, string text) =>
        UseColor ? $"{ansi}{text}{AnsiReset}" : text;

    public static string SymbolOf(Player p) => p switch
    {
        Player.Ascendant => Sun,
        Player.Vanguard  => Sparkle,
        _                => " "
    };

    public static string ColoredSymbolOf(Player p) => p switch
    {
        Player.Ascendant => Color(AnsiGold, Sun),
        Player.Vanguard  => Color(AnsiCrimson, Sparkle),
        _                => " "
    };

    public static string NameOf(Player p) => p switch
    {
        Player.Ascendant => AscendantName,
        Player.Vanguard  => VanguardName,
        _                => "Nobody"
    };

    public static string ColoredNameOf(Player p) => p switch
    {
        Player.Ascendant => Color(AnsiGold, AscendantName),
        Player.Vanguard  => Color(AnsiCrimson, VanguardName),
        _                => "Nobody"
    };

    public static string DifficultyName(Difficulty d) => d switch
    {
        Difficulty.Recruit => "Recruit",
        Difficulty.Captain => "Captain",
        Difficulty.Warlord => "Warlord",
        _                  => d.ToString()
    };

    public static string PlacementMessage(Player p, string sector) =>
        $"{ColoredNameOf(p)} claims sector {sector}.";

    public static string WinMessage(Player p) =>
        $"{ColoredNameOf(p)} holds the line — three sectors in a row.";

    public static string DrawMessage() =>
        "Stalemate. The galaxy holds its breath.";

    public static string OccupiedMessage(string sector) =>
        $"Sector {sector} is already claimed.";

    public static string InvalidCoordMessage() =>
        "Invalid coordinates, captain. Use A1 through C3.";
}
```

- [ ] **Step 2: Strip the placeholder symbol consts**

The two `AscendantSymbol`/`VanguardSymbol` placeholder constants in step 1 exist only to keep the file self-contained. Delete them now — `SymbolOf` is the only public way to get a symbol. Final state of those lines:

```csharp
// (lines deleted — Sun and Sparkle are the canonical glyphs)
```

- [ ] **Step 3: Build to confirm `Theme.cs` references types not yet defined**

Expected: build *fails* with `error CS0246: The type or namespace name 'Player' could not be found` and `error CS0246: The type or namespace name 'Difficulty' could not be found`. This is fine — Tasks 3 and 5 introduce these types. Move on.

---

## Task 3: Board state and rules

**Files:**
- Create: `./Board.cs`

- [ ] **Step 1: Write `Board.cs`**

```csharp
using System.Collections.Generic;

namespace TicTacToe;

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
        // Rows
        for (int r = 0; r < 3; r++)
            if (_cells[r, 0] != Player.None &&
                _cells[r, 0] == _cells[r, 1] &&
                _cells[r, 1] == _cells[r, 2])
                return _cells[r, 0];

        // Columns
        for (int c = 0; c < 3; c++)
            if (_cells[0, c] != Player.None &&
                _cells[0, c] == _cells[1, c] &&
                _cells[1, c] == _cells[2, c])
                return _cells[0, c];

        // Diagonals
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
```

- [ ] **Step 2: Build**

```powershell
dotnet build
```
Expected: still fails on `Difficulty` references in `Theme.cs`. That's resolved in Task 5.

---

## Task 4: Renderer (all console I/O)

**Files:**
- Create: `./Renderer.cs`

- [ ] **Step 1: Write `Renderer.cs`**

```csharp
using System;
using System.Text;

namespace TicTacToe;

public static class Renderer
{
    public static void Initialize()
    {
        Console.OutputEncoding = Encoding.UTF8;

        // Auto-disable color when output is redirected, NO_COLOR is set, or terminal looks dumb.
        bool disable =
            Console.IsOutputRedirected ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")) ||
            string.Equals(Environment.GetEnvironmentVariable("TERM"), "dumb", StringComparison.OrdinalIgnoreCase);

        Theme.UseColor = !disable;
    }

    public static void DrawTitle()
    {
        Console.WriteLine();
        Console.WriteLine(Theme.Color(Theme.AnsiBold,
            "═══ ORDER VS REBELLION — A SECTOR DUEL ═══"));
        Console.WriteLine($"  {Theme.ColoredNameOf(Player.Ascendant)} ({Theme.ColoredSymbolOf(Player.Ascendant)})  vs  {Theme.ColoredNameOf(Player.Vanguard)} ({Theme.ColoredSymbolOf(Player.Vanguard)})");
        Console.WriteLine();
    }

    public static void DrawBoard(Board board)
    {
        string Cell(int r, int c) => Theme.ColoredSymbolOf(board.At(r, c));

        Console.WriteLine();
        Console.WriteLine("       A   B   C");
        Console.WriteLine("     ┌───┬───┬───┐");
        Console.WriteLine($"   1 │ {Cell(0, 0)} │ {Cell(0, 1)} │ {Cell(0, 2)} │");
        Console.WriteLine("     ├───┼───┼───┤");
        Console.WriteLine($"   2 │ {Cell(1, 0)} │ {Cell(1, 1)} │ {Cell(1, 2)} │");
        Console.WriteLine("     ├───┼───┼───┤");
        Console.WriteLine($"   3 │ {Cell(2, 0)} │ {Cell(2, 1)} │ {Cell(2, 2)} │");
        Console.WriteLine("     └───┴───┴───┘");
        Console.WriteLine();
    }

    public static void DrawScore(int ascendantWins, int vanguardWins, int draws)
    {
        var a = $"{Theme.ColoredNameOf(Player.Ascendant)} {Theme.ColoredSymbolOf(Player.Ascendant)} {ascendantWins}";
        var v = $"{Theme.ColoredNameOf(Player.Vanguard)} {Theme.ColoredSymbolOf(Player.Vanguard)} {vanguardWins}";
        var d = $"Stalemates {draws}";
        Console.WriteLine($"  {a}   —   {v}   —   {d}");
    }

    public static void Announce(string message)
    {
        Console.WriteLine();
        Console.WriteLine($"  {message}");
        Console.WriteLine();
    }

    public static void Error(string message)
    {
        Console.WriteLine($"  {Theme.Color(Theme.AnsiCrimson, "!")}  {message}");
    }

    public static string ReadLine(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine() ?? string.Empty;
    }

    /// <summary>
    /// Parses "A1".."C3" (or "1A".."3C") into 0-based (row, col). Case- and whitespace-insensitive.
    /// Returns null on invalid input.
    /// </summary>
    public static (int row, int col)? ParseCoordinate(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        char letter = '\0';
        char digit  = '\0';

        foreach (char ch in input)
        {
            if (char.IsWhiteSpace(ch)) continue;
            if (char.IsLetter(ch))
            {
                if (letter != '\0') return null;
                letter = char.ToUpperInvariant(ch);
            }
            else if (char.IsDigit(ch))
            {
                if (digit != '\0') return null;
                digit = ch;
            }
            else
            {
                return null;
            }
        }

        if (letter < 'A' || letter > 'C') return null;
        if (digit  < '1' || digit  > '3') return null;

        int col = letter - 'A';
        int row = digit  - '1';
        return (row, col);
    }

    public static string SectorLabel(int row, int col)
        => $"{(char)('A' + col)}{row + 1}";

    public static (int row, int col) PromptForCoordinate(Player active, Board board)
    {
        while (true)
        {
            string line = ReadLine($"  {Theme.ColoredNameOf(active)} — claim a sector (A1–C3): ");
            var parsed = ParseCoordinate(line);
            if (parsed is null)
            {
                Error(Theme.InvalidCoordMessage());
                continue;
            }
            var (r, c) = parsed.Value;
            if (board.At(r, c) != Player.None)
            {
                Error(Theme.OccupiedMessage(SectorLabel(r, c)));
                continue;
            }
            return (r, c);
        }
    }

    public static int PromptMenu(string title, string[] options)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine($"  {Theme.Color(Theme.AnsiBold, title)}");
            for (int i = 0; i < options.Length; i++)
                Console.WriteLine($"    {i + 1}. {options[i]}");

            string line = ReadLine("  Select: ").Trim();
            if (int.TryParse(line, out int n) && n >= 1 && n <= options.Length)
                return n - 1;

            Error("Pick a number from the menu.");
        }
    }

    public static bool PromptYesNo(string question)
    {
        while (true)
        {
            string line = ReadLine($"  {question} (y/n): ").Trim().ToLowerInvariant();
            if (line is "y" or "yes") return true;
            if (line is "n" or "no")  return false;
            Error("Answer y or n.");
        }
    }
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build
```
Expected: still fails — `Theme.cs` references `Difficulty`, defined in Task 5.

---

## Task 5: AI — Recruit, Captain, Warlord

**Files:**
- Create: `./Ai.cs`

- [ ] **Step 1: Write `Ai.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace TicTacToe;

public enum Difficulty
{
    Recruit,  // random
    Captain,  // win > block > random
    Warlord   // full minimax
}

public static class Ai
{
    private static readonly Random Rng = new();

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
        // 1. Win if possible.
        if (TryFindWinningMove(board, self, out var winning)) return winning;

        // 2. Block opponent's immediate win.
        if (TryFindWinningMove(board, Board.Other(self), out var blocking)) return blocking;

        // 3. Otherwise random.
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

        // Defensive fallback (should never trigger because EmptyCells is non-empty here).
        return bestMove.row >= 0 ? bestMove : ChooseRandom(board);
    }

    /// <summary>
    /// Returns a score from `self`'s perspective.
    /// + (10 - depth) for self win, − (10 - depth) for opponent win, 0 for draw.
    /// Subtracting depth makes the AI prefer faster wins and slower losses.
    /// </summary>
    private static int Minimax(Board board, Player toMove, Player self, int depth)
    {
        var winner = board.Winner();
        if (winner == self)               return 10 - depth;
        if (winner == Board.Other(self))  return depth - 10;
        if (board.IsDraw())               return 0;

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
```

- [ ] **Step 2: Build the whole project**

```powershell
dotnet build
```
Expected: `Build succeeded. 0 Warning(s). 0 Error(s).` All cross-file types now resolve.

---

## Task 6: Wire `Program.cs` — menu, match loop, vs-CPU flow

**Files:**
- Modify: `./Program.cs` (replace placeholder)

- [ ] **Step 1: Replace `Program.cs` with the real entry point**

```csharp
using System;

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
            new[] { $"{Theme.AscendantName} ({Theme.Sun}) — moves first", $"{Theme.VanguardName} ({Theme.Sparkle}) — moves second" });
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
        Player active = Player.Ascendant; // Ascendant always moves first.

        while (true)
        {
            Renderer.DrawBoard(board);

            (int row, int col) move;
            if (opponentIsAi && active != humanSide)
            {
                move = Ai.ChooseMove(board, active, aiDifficulty);
                Renderer.Announce($"{Theme.ColoredNameOf(active)} ({Theme.DifficultyName(aiDifficulty)}) considers the grid…");
            }
            else
            {
                move = Renderer.PromptForCoordinate(active, board);
            }

            board.TryPlace(move.row, move.col, active);
            Renderer.Announce(Theme.PlacementMessage(active, Renderer.SectorLabel(move.row, move.col)));

            var winner = board.Winner();
            if (winner is { } w)
            {
                Renderer.DrawBoard(board);
                Renderer.Announce(Theme.WinMessage(w));
                return w == Player.Ascendant ? MatchResult.AscendantWins : MatchResult.VanguardWins;
            }
            if (board.IsDraw())
            {
                Renderer.DrawBoard(board);
                Renderer.Announce(Theme.DrawMessage());
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
```

- [ ] **Step 2: Build**

```powershell
dotnet build
```
Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 3: Spot-check a hot-seat run with piped input**

The match should: render the title, show menu, accept `1` (hot-seat), then play A1, A2, B1, B2, C1 → Ascendant wins on the top row.

```powershell
"1`r`nA1`r`nA2`r`nB1`r`nB2`r`nC1`r`nn`r`n3`r`n" | dotnet run --project .
```

(`\r\n` line endings via PowerShell backtick-r-backtick-n. Last `n` declines a rematch, `3` quits to exit.)

Expected stdout (substring matches):
- `═══ ORDER VS REBELLION` — title appears
- `claims sector A1` — first placement message
- `holds the line — three sectors in a row` — win message after C1
- `Stalemates 0` — score line shown
- exit code 0

If any step fails: re-read `Program.cs` and `Renderer.cs`, look for typos in coordinate parsing or win-detection ordering.

---

## Task 7: Smoke-test the full checklist

**Files:**
- (no source changes — runtime verification only)

- [ ] **Step 1: Hot-seat column win**

```powershell
"1`r`n1`r`nA1`r`nB1`r`nA2`r`nB2`r`nA3`r`nn`r`n3`r`n" | dotnet run --project .
```
Expected: Ascendant wins via A1/A2/A3, "holds the line" message, exit 0.

- [ ] **Step 2: Hot-seat diagonal win**

```powershell
"1`r`nA1`r`nA2`r`nB2`r`nA3`r`nC3`r`nn`r`n3`r`n" | dotnet run --project .
```
Expected: Ascendant wins via A1/B2/C3 diagonal.

- [ ] **Step 3: Draw**

A known stalemate sequence (Ascendant first):
A1, B1, C1, B2, A2, C2, B3, A3, C3 → no row, column, or diagonal completes for either side (verify by hand before running).

Actually the cleanest forced draw with both sides playing optimally is:
B2 (A), A1 (V), C3 (A), A3 (V), A2 (V cannot, swap), C1 (A), B1 (V), B3 (A), … messy.

Use this concrete sequence guaranteed to draw:
A: B2, V: A1, A: C1, V: A3, A: A2, V: C2, A: B1, V: B3, A: C3.

```powershell
"1`r`nB2`r`nA1`r`nC1`r`nA3`r`nA2`r`nC2`r`nB1`r`nB3`r`nC3`r`nn`r`n3`r`n" | dotnet run --project .
```
Expected: "Stalemate. The galaxy holds its breath.", `Stalemates 1` in the score.

- [ ] **Step 4: Invalid input recovers**

```powershell
"1`r`nZ9`r`n`r`nB4`r`nA1`r`n`r`nA1`r`nA2`r`nB1`r`nB2`r`nC1`r`nn`r`n3`r`n" | dotnet run --project .
```
Note line 2 (`Z9`), line 3 (empty), line 4 (`B4`) all reprompt. Line 7 retries `A1` after it's already taken (should print "already claimed"). Final result: Ascendant wins on top row.

Expected substrings:
- `Invalid coordinates, captain` (3 times)
- `is already claimed` (1 time)
- `holds the line` (final win)

- [ ] **Step 5: Versus-Warlord never loses**

Run 5 times — each time pick `2` (vs computer), `1` (be Ascendant), `3` (Warlord), then play whatever moves you like. Verify the human never wins. Use this as one quick automated probe (Ascendant just plays the first 5 empty cells, Warlord responds):

```powershell
"2`r`n1`r`n3`r`nA1`r`nA2`r`nA3`r`nB1`r`nB3`r`nC1`r`nC2`r`nC3`r`nn`r`n3`r`n" | dotnet run --project .
```
Expected: the Warlord wins or forces a draw — never `The Ascendant holds the line`.
(Some inputs may not all be consumed if the AI ends the game early; that's fine, extra inputs are ignored.)

- [ ] **Step 6: Captain blocks an immediate threat**

Set up a position where the human (Ascendant) has two-in-a-row and the AI must block:

```powershell
"2`r`n1`r`n2`r`nA1`r`nA2`r`nB1`r`nC1`r`nn`r`n3`r`n" | dotnet run --project .
```
Sequence: human A1, AI responds (anywhere), human B1 (now A1+B1 on top row → C1 threat), AI must play C1.

Expected: The output should contain `claims sector C1.` from `The Crimson Vanguard` *before* the human's third turn input (`C1`) is consumed. Check that the sector C1 is taken by the Vanguard ✦ in the next board render.

- [ ] **Step 7: NO_COLOR fallback**

```powershell
$env:NO_COLOR = "1"; "1`r`nA1`r`nB2`r`nA2`r`nB3`r`nA3`r`nn`r`n3`r`n" | dotnet run --project .; Remove-Item Env:\NO_COLOR
```
Expected: no `[` escape sequences anywhere in output. Symbols still render. Ascendant still wins on column A.

- [ ] **Step 8: Quit cleanly**

```powershell
"3`r`n" | dotnet run --project .
```
Expected: title appears, menu prompt, then exits with code 0. No board rendered.

---

## Task 8: Write `MORNING.md` summary for the user

**Files:**
- Create: `./MORNING.md`

- [ ] **Step 1: Write the morning summary**

Cover, in order:
1. **Status:** what's done, builds clean, smoke checklist results.
2. **Decisions made on your behalf** (from the spec's "Decisions" table — re-list the 6 items).
3. **What to look at first** if reviewing: `docs/superpowers/specs/...md`, then `docs/superpowers/plans/...md`, then `Program.cs`.
4. **How to play right now:** `dotnet run` from the project directory.
5. **What's NOT done / questions for you:**
   - Should we add a `git init` + initial commit?
   - Want to upgrade to "medium skin" (title splash, per-cell sector names, themed difficulty intro)?
   - Want a `TicTacToe.Tests` xUnit project added?
6. **Known limitations:** old terminals without UTF-8 will render `?` for ☼/✦.

Keep it under ~80 lines. Plain prose, no tables needed.

---

## Self-Review Checklist (run after writing all tasks)

- [x] **Spec coverage:** Every section of the design spec maps to a task: Architecture → Tasks 2-6, Game flow → Task 6, Theme → Task 2 + Task 4 (rendering), AI → Task 5, Error handling → Task 4 (parsing) + Task 6 (top-level catch), Testing → Task 7 (smoke), Out-of-scope items omitted.
- [x] **Placeholder scan:** No TBD/TODO. All code blocks contain real code.
- [x] **Type consistency:** `Player`, `Difficulty`, `Board`, `Theme.UseColor`, `Renderer.Initialize` used identically across tasks. `ChooseMove` signature matches between Task 5 definition and Task 6 call site.
- [x] **Build sequencing:** Tasks 2-4 deliberately leave the build in a non-compiling state (since `Theme.cs` references types defined later). Task 5 closes the gap; the plan tells the engineer to expect this.

---

## Execution

Plan complete. The user is asleep and explicitly authorized autonomous execution. Proceeding with **inline execution** in this session — single agent, walk task-by-task, build/run after each, stop on any unexpected failure for human review in the morning.
