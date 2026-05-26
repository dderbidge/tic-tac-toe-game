# Tic-Tac-Toe — "Order vs Rebellion" Console Game

**Date:** 2026-05-05
**Author:** dderbidge
**Status:** Design (pre-implementation)

## Overview

A C# console tic-tac-toe game with an original space-opera theme — "The Ascendant" (☼) versus "The Crimson Vanguard" (✦) — playable as hot-seat or against an AI with three difficulty tiers. The theme is a *light skin*: custom symbols, faction names, ANSI colors, and themed flavor strings. No trademarked names, characters, or silhouettes. Targets .NET 9, Windows-first but cross-platform-compatible.

## Goals & Non-Goals

**Goals**
- A finished, playable console game in a single session of work.
- Clean separation of concerns so each file is small and testable in isolation.
- Theme that feels distinctive without crossing into Lucasfilm/Disney IP.
- AI opponent that's actually fun: easy mode is beatable, hard mode is unbeatable.

**Non-Goals**
- Graphical UI, animations, or sound files.
- Network/multiplayer.
- Persisted scores or save files.
- Localization, accessibility tooling, or telemetry.
- A separate test project (the user picked "B. Multi-file, single project" — manual smoke checklist instead).

## Theme Vocabulary

All names below are original — none reference protected marks.

| Concept                    | Light skin name                          |
|---------------------------|------------------------------------------|
| First faction (X)          | **The Ascendant** — symbol ☼ (gold)      |
| Second faction (O)         | **The Crimson Vanguard** — symbol ✦ (red)|
| The board                  | "Sector grid" / "targeting grid"         |
| A cell                     | "Sector" (B2 = "sector B2")              |
| Place a piece              | "Claim a sector"                         |
| Win                        | "Holds the line — three sectors in a row"|
| Loss                       | "Sector lost. The line is broken."       |
| Draw                       | "Stalemate. The galaxy holds its breath."|
| AI difficulty (easy/med/hard) | **Recruit / Captain / Warlord**       |

## Architecture

Single project: `TicTacToe.csproj` (top-level statements off, `Main` method explicit). Five source files, each with one job.

### `Program.cs` — Entry & control flow
- Reads the menu (Hot-seat / vs Computer / Quit).
- For *vs Computer*: prompts which faction the human plays and the difficulty tier.
- Drives the match loop: render → prompt → place → check end → swap → repeat.
- Tracks per-session score across rematches.
- Exits cleanly on user choice; Ctrl+C uses default .NET behavior.

### `Board.cs` — Game state and rules (no I/O)
```csharp
public enum Player { None, Ascendant, Vanguard }

public sealed class Board
{
    private readonly Player[,] _cells = new Player[3, 3];
    public Player At(int row, int col);
    public bool TryPlace(int row, int col, Player p);   // false if occupied/out-of-range
    public Player? Winner();                             // null if no winner yet
    public bool IsDraw();                                // no winner, no empty cells
    public bool IsGameOver();                            // Winner() != null || IsDraw()
    public IEnumerable<(int row, int col)> EmptyCells();
    public Board Clone();                                // for AI lookahead
}
```
Pure logic. Easy to unit-test later if the user adds project C.

### `Ai.cs` — Difficulty tiers
```csharp
public enum Difficulty { Recruit, Captain, Warlord }

public static class Ai
{
    public static (int row, int col) ChooseMove(Board board, Player self, Difficulty d);
}
```
- **Recruit:** picks a uniformly random empty cell.
- **Captain:** if a winning move exists for `self`, take it; else if opponent has a one-move win, block it; else random.
- **Warlord:** full minimax. Tic-tac-toe's full game tree is small enough (under ~5,500 reachable positions) that alpha-beta pruning isn't needed for performance, but it's a few extra lines if we want it.

### `Renderer.cs` — All console output
- Owns every `Console.Write` / `Console.WriteLine` / `Console.ReadLine` call.
- ANSI color helpers (gold = `\x1b[33m`, crimson = `\x1b[31m`, dim = `\x1b[2m`, reset = `\x1b[0m`).
- `DrawBoard(Board)`, `DrawTitle()`, `DrawScore(int ascendantWins, int vanguardWins, int draws)`, `DrawMenu()`, `Prompt(string)`, `Error(string)`, `Announce(string)`.
- On startup, sets `Console.OutputEncoding = System.Text.Encoding.UTF8` for ☼ and ✦.
- ANSI auto-detection: if `Console.IsOutputRedirected` is true, or `NO_COLOR` env var is set, emits no escape codes.

### `Theme.cs` — Strings, symbols, colors (one place to retheme)
- `const string` faction names, symbol glyphs, ANSI color sequences.
- Static helper `string Flavor.PlacementMessage(Player p, string sector)` returning e.g. *"The Ascendant claims sector B2."*
- `string Flavor.WinMessage(Player p)`, `string Flavor.DrawMessage()`.
- Difficulty display names ("Recruit", "Captain", "Warlord").

## Game Flow

```
[Title banner]
        │
        ▼
   ┌──Main Menu──┐
   │ 1. Hot-seat │
   │ 2. vs CPU   │
   │ 3. Quit     │
   └─────┬───────┘
         │
   (if vs CPU)
   pick faction → pick difficulty
         │
         ▼
   Match loop:
     render board + score
     prompt active player ("Ascendant — claim a sector (A1–C3): ")
     parse input  ─── invalid? show error, loop
     TryPlace     ─── occupied? show error, loop
     check end:
       winner   → announce, increment score
       draw     → announce, increment score
       else     → swap player, repeat
         │
         ▼
   "Play again? (y/n)"  ── y → match loop
                        └─ n → main menu
```

### Input format
- Coordinate strings `A1`, `B2`, `c3` — case-insensitive, optionally with whitespace.
- Column letter `A|B|C`, row digit `1|2|3`. Either order accepted (`A1` or `1A`).
- Anything else: themed error *"Invalid coordinates, captain. Use A1 through C3."*
- Empty input: same error, reprompt.

### Board layout (rendered)

```
       A   B   C
     ┌───┬───┬───┐
   1 │ ☼ │   │ ✦ │
     ├───┼───┼───┤
   2 │   │ ☼ │   │
     ├───┼───┼───┤
   3 │ ✦ │   │   │
     └───┴───┴───┘
```

Symbols colored inline:
- `☼` rendered with gold ANSI (`\x1b[33m☼\x1b[0m`)
- `✦` rendered with crimson ANSI (`\x1b[31m✦\x1b[0m`)

Score line beneath the board:
```
Ascendant ☼ 2  —  Crimson Vanguard ✦ 1  —  Stalemates 0
```

## Decisions Made Without User Input (flagged for review)

| # | Decision | Why this default | How to override |
|---|----------|------------------|-----------------|
| 1 | Ascendant ☼ always moves first | Classic "X-first" convention; simpler than rotating starts | Toggle in `Program.cs`: alternate starting faction each match |
| 2 | Coordinate input is `A1`–`C3` (column letter, row digit) | More themed than 1–9; avoids ambiguity with difficulty selection | Replace parser in `Renderer.ReadCoordinate` with 1–9 numeric |
| 3 | Score persists per-session only, not to disk | Matches the "light skin" scope choice | Add JSON file in user profile dir under `Theme.cs` |
| 4 | ANSI colors auto-disable on output redirection or `NO_COLOR` env var; otherwise on | Standard de-facto convention; works in modern Windows Terminal/PowerShell | Force-on flag in `Theme.UseColor` |
| 5 | In vs-CPU mode, the human picks faction first, then difficulty | Faction is the more thematic choice — feels like the lead question | Reorder prompts in `Program.RunVersusComputer` |
| 6 | Difficulty names are "Recruit / Captain / Warlord" | Themed, memorable, ordered by clear progression | Edit `Theme.DifficultyNames` |

## Error Handling

- Invalid coordinate input → reprompt, no exception. Themed error message.
- Already-occupied sector → reprompt with *"Sector B2 is already claimed."*
- Unexpected exceptions in `Main` → catch at top level, print *"A subspace fault has interrupted the game."* and the exception message; exit code 1. (Defensive only — none expected in normal flow.)
- Console doesn't support UTF-8 (very old terminals): symbols render as `?`. Acceptable degradation; we don't detect.

## Testing Strategy (manual smoke checklist)

The user picked structure B (no test project). Before declaring v1 done, run through:

1. **Build:** `dotnet build` succeeds with no warnings.
2. **Hot-seat row win:** Ascendant claims A1, B1, C1 across turns → "holds the line" message, score `1 — 0 — 0`.
3. **Hot-seat column win:** Vanguard wins via A1/A2/A3 → score increments correctly.
4. **Hot-seat diagonal win:** Win via A1/B2/C3 → detected.
5. **Draw:** Play out a known stalemate sequence → "Stalemate" message, draws score `1`.
6. **Recruit AI:** Plays *something* in every empty cell over enough trials (sanity).
7. **Captain AI:** Set up a position where opponent has 2-in-a-row → AI plays the block.
8. **Warlord AI:** Play 5 games — AI never loses (may draw if optimal).
9. **Invalid input:** Type `Z9`, empty enter, `B4` — all reprompt, no crash.
10. **Occupied cell:** Try to claim a taken sector → themed error, reprompt.
11. **Color fallback:** Run with `NO_COLOR=1` set — no escape codes in output.
12. **Replay loop:** Win a match, choose `y` → new board, score persists. Choose `n` → back to main menu.
13. **Quit:** Main menu → 3 → exits with code 0.

A future iteration could promote a subset of these to xUnit tests in a `TicTacToe.Tests` project.

## Out of Scope (deferred)

- Title-screen ASCII faction sigils (would belong in a "medium skin" upgrade).
- Match-history persistence to disk.
- Network play / online matchmaking.
- Cross-platform terminal capability detection beyond `NO_COLOR`.
- Sound (console beeps).
- A graphical front-end.

## File Inventory at Completion

```
tic-tac-toe-game/
├── docs/superpowers/specs/2026-05-05-tic-tac-toe-design.md   (this file)
├── docs/superpowers/plans/2026-05-05-tic-tac-toe-plan.md     (next step)
├── TicTacToe.csproj
├── Program.cs
├── Board.cs
├── Ai.cs
├── Renderer.cs
├── Theme.cs
└── MORNING.md   (summary of what was built overnight)
```
