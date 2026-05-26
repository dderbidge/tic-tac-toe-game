# Tic-Tac-Toe Web — "Cosmic Sigils" Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor the existing console game's logic into a shared `core/` class library, then build a new Blazor WebAssembly PWA at `web/` with the Cosmic Sigils visual theme — installable on desktop & mobile, fully offline-capable, with placeholder SVG/CSS assets that can be swapped for PNGs in Phase 2.

**Architecture:** Three sibling .NET projects under the repo root tied by `TicTacToe.sln`: `core/` (class library — Board, Ai, enums, theme strings), `console/` (existing console UI, refactored to reference core), `web/` (new Blazor WASM PWA referencing core). The web app uses a singleton `GameSession` service for state and a small set of Razor components for the state machine (MainMenu → ModeSetup → InMatch → MatchOver). All visual customization lives in three CSS files plus a `wwwroot/img/` asset folder with PNG-swap-ready CSS custom properties.

**Tech Stack:** .NET 9 SDK, C# 12, Blazor WebAssembly with PWA template, Razor components, scoped CSS, `IJSRuntime` for `localStorage`, no third-party NuGet dependencies. Placeholder visuals: inline SVG and pure CSS gradients. Phase 2 visuals: PNG files dropped into `web/wwwroot/img/`.

**Note on testing:** The existing console smoke checklist (`console/tests/smoke.ps1`, 10 scenarios) MUST keep passing 10/10 after the refactor — that's the regression bar. The web app v1 has no automated tests by design (per the spec); each task that produces UI ends with a runtime verification step (build clean, page loads, click flow works in browser).

---

## File Inventory

| File | Status | Responsibility |
|---|---|---|
| `TicTacToe.sln` | NEW | Ties core/console/web together |
| `core/TicTacToe.Core.csproj` | NEW | Class library project file |
| `core/Player.cs` | NEW (extracted) | Player enum |
| `core/Difficulty.cs` | NEW (extracted) | Difficulty enum |
| `core/Board.cs` | MOVED from `console/` | Board state + rules |
| `core/Ai.cs` | MOVED from `console/` | AI difficulty tiers |
| `core/ThemeText.cs` | NEW (extracted) | Faction names, symbols, flavor strings |
| `console/TicTacToe.Console.csproj` | RENAMED from `TicTacToe.csproj` | Console project file |
| `console/ConsolePalette.cs` | NEW (extracted) | ANSI codes + UseColor flag |
| `console/Theme.cs` | DELETED | Split into `core/ThemeText.cs` + `console/ConsolePalette.cs` |
| `console/Renderer.cs` | MODIFIED | Uses `ConsolePalette` and `ThemeText` instead of old `Theme` |
| `console/Program.cs` | MODIFIED | Adds `using TicTacToe.Core;` |
| `web/TicTacToe.Web.csproj` | NEW | Blazor WASM project file |
| `web/Program.cs` | NEW | App entry, DI setup |
| `web/App.razor`, `web/_Imports.razor`, `web/MainLayout.razor` | NEW | Boilerplate from template |
| `web/Components/Game.razor` | NEW | Root component, state machine |
| `web/Components/MainMenu.razor` | NEW | Mode selection |
| `web/Components/ModeSetup.razor` | NEW | Faction + difficulty pick |
| `web/Components/BoardView.razor` | NEW | 3×3 grid |
| `web/Components/Cell.razor` | NEW | Single cell |
| `web/Components/ScoreBoard.razor` | NEW | Score panel + reset |
| `web/Services/GameSession.cs` | NEW | State, AI invocation, localStorage |
| `web/wwwroot/index.html` | MODIFIED from template | Cosmic theme head meta |
| `web/wwwroot/manifest.webmanifest` | MODIFIED from template | App name, theme colors, icons |
| `web/wwwroot/css/theme.css` | NEW | Palette, typography, asset slots |
| `web/wwwroot/css/board.css` | NEW | Grid + cell layout |
| `web/wwwroot/css/animations.css` | NEW | Placement, pulse, win-line |
| `web/wwwroot/img/ascendant-sigil.svg` | NEW | Placeholder sun sigil |
| `web/wwwroot/img/vanguard-sigil.svg` | NEW | Placeholder sparkle sigil |
| `web/wwwroot/img/README.md` | NEW | Phase 2 PNG asset spec |
| `web/wwwroot/img/icons/icon-{192,512}.png` | NEW (placeholders) | App icons |
| `web/wwwroot/img/icons/icon-maskable-{192,512}.png` | NEW (placeholders) | Maskable variants |
| `web/wwwroot/img/icons/apple-touch-icon.png` | NEW (placeholder) | iOS icon |
| `web/wwwroot/favicon.ico` | NEW (placeholder) | Browser tab |

---

## Task 1: Create solution and core/ class library

**Files:**
- Create: `./TicTacToe.sln`
- Create: `./core/TicTacToe.Core.csproj`

- [ ] **Step 1: Create the empty class library skeleton**

Run from `.`:
```powershell
dotnet new classlib --name TicTacToe.Core --output core --framework net9.0
```
Expected: creates `core/TicTacToe.Core.csproj` and a stub `core/Class1.cs`. The csproj should already have `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`.

- [ ] **Step 2: Delete the stub class file**

```powershell
Remove-Item core/Class1.cs
```

- [ ] **Step 3: Create the solution file and add both existing projects + the new core**

```powershell
dotnet new sln --name TicTacToe
dotnet sln TicTacToe.sln add core/TicTacToe.Core.csproj
dotnet sln TicTacToe.sln add console/TicTacToe.csproj
```

(Don't worry — we'll rename the console csproj in Task 4.)

- [ ] **Step 4: Verify the solution builds**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: `Build succeeded. 0 Warning(s). 0 Error(s).` Both projects build (core is empty, console builds as before).

---

## Task 2: Move Board.cs and Ai.cs into core/

**Files:**
- Move: `./console/Board.cs` → `./core/Board.cs`
- Move: `./console/Ai.cs` → `./core/Ai.cs`

- [ ] **Step 1: Move the files (keep namespace change for the next step)**

```powershell
Move-Item console/Board.cs core/Board.cs
Move-Item console/Ai.cs core/Ai.cs
```

- [ ] **Step 2: Update namespace in `core/Board.cs`**

Change the second line from:
```csharp
namespace TicTacToe;
```
to:
```csharp
namespace TicTacToe.Core;
```
Leave everything else identical.

- [ ] **Step 3: Update namespace in `core/Ai.cs`**

Same change: `namespace TicTacToe;` → `namespace TicTacToe.Core;`. Leave the rest (including the `TICTACTOE_SEED` env-var support) untouched.

- [ ] **Step 4: Add a project reference from console to core**

Edit `console/TicTacToe.csproj`. Inside the existing `<Project>` element, add a new `<ItemGroup>` containing the project reference (or extend an existing one):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\core\TicTacToe.Core.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5: Add `using TicTacToe.Core;` to console files that reference moved types**

Three files now need it: `console/Program.cs`, `console/Renderer.cs`, `console/Theme.cs`.

In `console/Program.cs`, after the existing `using` lines, add:
```csharp
using TicTacToe.Core;
```

In `console/Renderer.cs`, same:
```csharp
using TicTacToe.Core;
```

In `console/Theme.cs`, same:
```csharp
using TicTacToe.Core;
```

- [ ] **Step 6: Verify the solution still builds**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

---

## Task 3: Extract `ThemeText.cs` to core/, create `ConsolePalette.cs` in console/

The current `console/Theme.cs` mixes platform-agnostic strings (faction names, symbols, flavor messages) with console-specific bits (ANSI codes, `UseColor`, `Color()` helper, `ColoredSymbolOf`, `ColoredNameOf`). Split them.

**Files:**
- Create: `./core/ThemeText.cs`
- Create: `./console/ConsolePalette.cs`
- Delete: `./console/Theme.cs`

- [ ] **Step 1: Create `core/ThemeText.cs` with the platform-agnostic content**

```csharp
namespace TicTacToe.Core;

public static class ThemeText
{
    public const string AscendantName = "The Ascendant";
    public const string VanguardName  = "The Crimson Vanguard";

    public const string Sun     = "☼";
    public const string Sparkle = "✦";

    public static string SymbolOf(Player p) => p switch
    {
        Player.Ascendant => Sun,
        Player.Vanguard  => Sparkle,
        _                => " "
    };

    public static string NameOf(Player p) => p switch
    {
        Player.Ascendant => AscendantName,
        Player.Vanguard  => VanguardName,
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
        $"{NameOf(p)} claims sector {sector}.";

    public static string WinMessage(Player p) =>
        $"{NameOf(p)} holds the line — three sectors in a row.";

    public static string DrawMessage() =>
        "Stalemate. The galaxy holds its breath.";

    public static string OccupiedMessage(string sector) =>
        $"Sector {sector} is already claimed.";

    public static string InvalidCoordMessage() =>
        "Invalid coordinates, captain. Use A1 through C3.";
}
```

Note: `ThemeText.PlacementMessage`, `WinMessage`, `NameOf` use plain (uncolored) names. The console wraps them with ANSI in `ConsolePalette`.

- [ ] **Step 2: Create `console/ConsolePalette.cs` with the ANSI-specific content**

```csharp
using TicTacToe.Core;

namespace TicTacToe;

public static class ConsolePalette
{
    public static bool UseColor { get; set; } = true;

    public const string AnsiReset   = "\x1b[0m";
    public const string AnsiGold    = "\x1b[33m";
    public const string AnsiCrimson = "\x1b[31m";
    public const string AnsiDim     = "\x1b[2m";
    public const string AnsiBold    = "\x1b[1m";

    public static string Color(string ansi, string text) =>
        UseColor ? $"{ansi}{text}{AnsiReset}" : text;

    public static string ColoredSymbolOf(Player p) => p switch
    {
        Player.Ascendant => Color(AnsiGold, ThemeText.Sun),
        Player.Vanguard  => Color(AnsiCrimson, ThemeText.Sparkle),
        _                => " "
    };

    public static string ColoredNameOf(Player p) => p switch
    {
        Player.Ascendant => Color(AnsiGold, ThemeText.AscendantName),
        Player.Vanguard  => Color(AnsiCrimson, ThemeText.VanguardName),
        _                => "Nobody"
    };

    public static string PlacementMessage(Player p, string sector) =>
        $"{ColoredNameOf(p)} claims sector {sector}.";

    public static string WinMessage(Player p) =>
        $"{ColoredNameOf(p)} holds the line — three sectors in a row.";
}
```

- [ ] **Step 3: Delete `console/Theme.cs`**

```powershell
Remove-Item console/Theme.cs
```

- [ ] **Step 4: Update `console/Renderer.cs` to use the new types**

Find every reference to `Theme.` in `console/Renderer.cs` and replace per this table:

| Old call | New call |
|---|---|
| `Theme.UseColor` | `ConsolePalette.UseColor` |
| `Theme.Color(...)` | `ConsolePalette.Color(...)` |
| `Theme.ColoredSymbolOf(...)` | `ConsolePalette.ColoredSymbolOf(...)` |
| `Theme.ColoredNameOf(...)` | `ConsolePalette.ColoredNameOf(...)` |
| `Theme.AnsiReset` / `AnsiGold` / `AnsiCrimson` / `AnsiDim` / `AnsiBold` | `ConsolePalette.<same>` |
| `Theme.AscendantName` / `VanguardName` / `Sun` / `Sparkle` | `ThemeText.<same>` |
| `Theme.SymbolOf(...)` / `NameOf(...)` | `ThemeText.<same>` |
| `Theme.DifficultyName(...)` | `ThemeText.DifficultyName(...)` |
| `Theme.OccupiedMessage(...)` / `InvalidCoordMessage()` | `ThemeText.<same>` |
| `Theme.PlacementMessage(...)` / `WinMessage(...)` | `ConsolePalette.<same>` (the colored versions) |
| `Theme.DrawMessage()` | `ThemeText.DrawMessage()` |

Make sure `using TicTacToe.Core;` is at the top of `Renderer.cs`. The new `ConsolePalette` lives in the `TicTacToe` namespace (no extra using needed since `Renderer.cs` is also `namespace TicTacToe`).

- [ ] **Step 5: Update `console/Program.cs` similarly**

Same replacement table as Step 4. `Program.cs` references `Theme.AscendantName`, `Theme.Sun`, `Theme.VanguardName`, `Theme.Sparkle`, `Theme.ColoredNameOf`, `Theme.DifficultyName`, `Theme.PlacementMessage`, `Theme.WinMessage`, `Theme.DrawMessage` — apply the table.

Also ensure `Renderer.Initialize()` (called from `Main`) sets `ConsolePalette.UseColor` rather than `Theme.UseColor` — that change happens in `Renderer.cs` Step 4.

- [ ] **Step 6: Verify build is clean**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

---

## Task 4: Rename console csproj and verify smoke tests pass 10/10

**Files:**
- Rename: `console/TicTacToe.csproj` → `console/TicTacToe.Console.csproj`
- Modify: `TicTacToe.sln` (sln entry must point to the renamed file)

- [ ] **Step 1: Remove the old console project entry from the solution**

```powershell
dotnet sln TicTacToe.sln remove console/TicTacToe.csproj
```

- [ ] **Step 2: Rename the csproj file**

```powershell
Rename-Item console/TicTacToe.csproj TicTacToe.Console.csproj
```

- [ ] **Step 3: Re-add the renamed project to the solution**

```powershell
dotnet sln TicTacToe.sln add console/TicTacToe.Console.csproj
```

- [ ] **Step 4: Verify build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 5: Run the console smoke checklist**

```powershell
cd console
pwsh -NoProfile -ExecutionPolicy Bypass -File tests/smoke.ps1
cd ..
```
Expected: `10 / 10 scenarios passed.` Exit code 0.

If anything fails: revert that scenario, look for a missed `Theme.*` → `ConsolePalette.*` / `ThemeText.*` swap. Do **not** advance to Task 5 until smoke passes 10/10.

---

## Task 5: Scaffold Blazor WebAssembly PWA project at `web/`

**Files:**
- Create: `./web/TicTacToe.Web.csproj` (via `dotnet new`)
- Create: `./web/wwwroot/...` (via `dotnet new`)
- Modify: `./TicTacToe.sln`

- [ ] **Step 1: Scaffold the project with PWA enabled**

```powershell
dotnet new blazorwasm --name TicTacToe.Web --output web --framework net9.0 --pwa
```
Expected: creates `web/TicTacToe.Web.csproj`, `web/Program.cs`, `web/App.razor`, `web/Pages/`, `web/wwwroot/manifest.webmanifest`, `web/wwwroot/service-worker.js`, `web/wwwroot/service-worker.published.js`, etc.

- [ ] **Step 2: Add the new project to the solution**

```powershell
dotnet sln TicTacToe.sln add web/TicTacToe.Web.csproj
```

- [ ] **Step 3: Add a project reference from web to core**

Edit `web/TicTacToe.Web.csproj`. Add this `<ItemGroup>` (alongside any existing ones):

```xml
<ItemGroup>
  <ProjectReference Include="..\core\TicTacToe.Core.csproj" />
</ItemGroup>
```

- [ ] **Step 4: Strip the boilerplate that ships with the template**

Remove these to start clean:

```powershell
Remove-Item web/Pages/Counter.razor
Remove-Item web/Pages/FetchData.razor
Remove-Item web/Pages/Index.razor
Remove-Item web/Pages/Home.razor -ErrorAction SilentlyContinue
Remove-Item web/wwwroot/sample-data -Recurse -ErrorAction SilentlyContinue
```

(Some templates use `Index.razor`, others `Home.razor` — try both, ignore errors for whichever doesn't exist.)

- [ ] **Step 5: Verify the solution builds**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: `Build succeeded. 0 Warning(s). 0 Error(s).` (Some warnings about missing routes are OK at this stage; we'll fix them via Game.razor in a later task. If errors prevent build, restore one of the stripped pages temporarily.)

If errors block the build because no routable page exists, create a minimal stub at `web/Pages/Home.razor`:
```razor
@page "/"

<h1>Tic-Tac-Toe Web — coming up</h1>
```
This stub is replaced entirely in Task 11.

---

## Task 6: Configure PWA manifest and `index.html` for the Cosmic theme

**Files:**
- Modify: `web/wwwroot/manifest.webmanifest`
- Modify: `web/wwwroot/index.html`

- [ ] **Step 1: Replace the manifest contents**

Overwrite `web/wwwroot/manifest.webmanifest` with:

```json
{
  "name": "Order vs Rebellion",
  "short_name": "Sector Duel",
  "description": "A duel of cosmic sigils.",
  "id": "/",
  "start_url": "/",
  "scope": "/",
  "display": "standalone",
  "orientation": "any",
  "theme_color": "#0a0518",
  "background_color": "#0a0518",
  "icons": [
    { "src": "img/icons/icon-192.png", "sizes": "192x192", "type": "image/png" },
    { "src": "img/icons/icon-512.png", "sizes": "512x512", "type": "image/png" },
    { "src": "img/icons/icon-maskable-192.png", "sizes": "192x192", "type": "image/png", "purpose": "maskable" },
    { "src": "img/icons/icon-maskable-512.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable" }
  ]
}
```

- [ ] **Step 2: Update `web/wwwroot/index.html` `<head>` for cosmic theming**

Open `web/wwwroot/index.html` and replace the `<head>` element with:

```html
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover" />
    <title>Order vs Rebellion</title>
    <base href="/" />

    <meta name="theme-color" content="#0a0518" />
    <meta name="description" content="A duel of cosmic sigils — tic-tac-toe in the Order vs Rebellion universe." />

    <link rel="manifest" href="manifest.webmanifest" />
    <link rel="apple-touch-icon" sizes="180x180" href="img/icons/apple-touch-icon.png" />
    <link rel="icon" type="image/x-icon" href="favicon.ico" />

    <meta name="apple-mobile-web-app-capable" content="yes" />
    <meta name="apple-mobile-web-app-status-bar-style" content="black-translucent" />
    <meta name="apple-mobile-web-app-title" content="Sector Duel" />

    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Spectral:ital,wght@0,400;0,600;1,400&family=Inter:wght@400;600&display=swap" rel="stylesheet" />

    <link rel="stylesheet" href="css/theme.css" />
    <link rel="stylesheet" href="css/board.css" />
    <link rel="stylesheet" href="css/animations.css" />
</head>
```

The three CSS files don't exist yet — they'll be created in Tasks 12–14. The Blazor template provides its own `app.css`; we'll leave that file alone (template requirement) but our overrides come last, so they win.

- [ ] **Step 3: Build to confirm no syntax errors in `index.html`**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: builds cleanly.

(The CSS files referenced will 404 in the browser until later tasks create them — that's fine for now.)

---

## Task 7: Implement `GameSession` service

**Files:**
- Create: `./web/Services/GameSession.cs`

- [ ] **Step 1: Create the directory**

```powershell
New-Item -ItemType Directory -Path web/Services
```

- [ ] **Step 2: Write `web/Services/GameSession.cs`**

```csharp
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using TicTacToe.Core;

namespace TicTacToe.Web.Services;

public enum GamePhase
{
    MainMenu,
    ModeSetup,
    InMatch,
    MatchOver
}

public sealed class GameSession
{
    private readonly IJSRuntime _js;
    private const string ScoresKey = "tictactoe.scores.v1";
    private const int AiDeliberationMs = 600;

    public GameSession(IJSRuntime js) { _js = js; }

    // ── State ────────────────────────────────────────────────────────────
    public GamePhase Phase { get; private set; } = GamePhase.MainMenu;
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

    // ── Score persistence ────────────────────────────────────────────────
    public async Task LoadScoresAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", ScoresKey);
            if (string.IsNullOrEmpty(json)) return;
            var s = JsonSerializer.Deserialize<ScoreDto>(json);
            if (s is null) return;
            AscendantWins = s.ascendant;
            VanguardWins  = s.vanguard;
            Draws         = s.draws;
            NotifyChanged();
        }
        catch { /* malformed value — keep zeros */ }
    }

    private async Task SaveScoresAsync()
    {
        var json = JsonSerializer.Serialize(new ScoreDto
        {
            ascendant = AscendantWins,
            vanguard  = VanguardWins,
            draws     = Draws
        });
        await _js.InvokeVoidAsync("localStorage.setItem", ScoresKey, json);
    }

    public async Task ResetScoresAsync()
    {
        AscendantWins = VanguardWins = Draws = 0;
        await SaveScoresAsync();
        NotifyChanged();
    }

    private sealed class ScoreDto
    {
        public int ascendant { get; set; }
        public int vanguard  { get; set; }
        public int draws     { get; set; }
    }

    // ── Mode setup ───────────────────────────────────────────────────────
    public void StartHotSeat()
    {
        VersusComputer = false;
        BeginMatch();
    }

    public void GoToModeSetup()
    {
        Phase = GamePhase.ModeSetup;
        NotifyChanged();
    }

    public async Task StartVersusComputerAsync(Player humanSide, Difficulty difficulty)
    {
        VersusComputer = true;
        HumanSide      = humanSide;
        AiDifficulty   = difficulty;
        BeginMatch();
        // If the AI moves first, kick its turn.
        if (Active != HumanSide) await PlayAiTurnAsync();
    }

    private void BeginMatch()
    {
        Board = new Board();
        Active = Player.Ascendant;
        Winner = null;
        IsDraw = false;
        WinningCells = null;
        Phase = GamePhase.InMatch;
        NotifyChanged();
    }

    public void GoToMainMenu()
    {
        Phase = GamePhase.MainMenu;
        NotifyChanged();
    }

    public async Task PlayAgainAsync()
    {
        BeginMatch();
        if (VersusComputer && Active != HumanSide) await PlayAiTurnAsync();
    }

    // ── Match flow ───────────────────────────────────────────────────────
    public async Task TryHumanMoveAsync(int row, int col)
    {
        if (Phase != GamePhase.InMatch) return;
        if (VersusComputer && Active != HumanSide) return;
        if (!Board.TryPlace(row, col, Active)) return;

        if (CheckEnd()) return;
        Active = Board.Other(Active);
        NotifyChanged();

        if (VersusComputer) await PlayAiTurnAsync();
    }

    private async Task PlayAiTurnAsync()
    {
        if (Phase != GamePhase.InMatch) return;
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

    private bool CheckEnd()
    {
        var winner = Board.Winner();
        if (winner is { } w)
        {
            Winner = w;
            WinningCells = ComputeWinningCells();
            if (w == Player.Ascendant) AscendantWins++; else VanguardWins++;
            Phase = GamePhase.MatchOver;
            _ = SaveScoresAsync();
            NotifyChanged();
            return true;
        }
        if (Board.IsDraw())
        {
            IsDraw = true;
            Draws++;
            Phase = GamePhase.MatchOver;
            _ = SaveScoresAsync();
            NotifyChanged();
            return true;
        }
        return false;
    }

    private (int row, int col)[]? ComputeWinningCells()
    {
        for (int r = 0; r < 3; r++)
        {
            if (Board.At(r, 0) != Player.None &&
                Board.At(r, 0) == Board.At(r, 1) &&
                Board.At(r, 1) == Board.At(r, 2))
                return new[] { (r, 0), (r, 1), (r, 2) };
        }
        for (int c = 0; c < 3; c++)
        {
            if (Board.At(0, c) != Player.None &&
                Board.At(0, c) == Board.At(1, c) &&
                Board.At(1, c) == Board.At(2, c))
                return new[] { (0, c), (1, c), (2, c) };
        }
        if (Board.At(0, 0) != Player.None &&
            Board.At(0, 0) == Board.At(1, 1) &&
            Board.At(1, 1) == Board.At(2, 2))
            return new[] { (0, 0), (1, 1), (2, 2) };
        if (Board.At(0, 2) != Player.None &&
            Board.At(0, 2) == Board.At(1, 1) &&
            Board.At(1, 1) == Board.At(2, 0))
            return new[] { (0, 2), (1, 1), (2, 0) };
        return null;
    }
}
```

- [ ] **Step 3: Register `GameSession` in `web/Program.cs`**

Open `web/Program.cs` and add the registration. The full file should look like:

```csharp
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TicTacToe.Web;
using TicTacToe.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<GameSession>();

await builder.Build().RunAsync();
```

- [ ] **Step 4: Add the using to `web/_Imports.razor`**

Append to `web/_Imports.razor`:
```razor
@using TicTacToe.Core
@using TicTacToe.Web.Services
@using TicTacToe.Web.Components
```

(Components/ folder will exist after Task 8.)

- [ ] **Step 5: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: builds cleanly. (The Components-folder using will warn but not error since the folder doesn't exist yet; it'll resolve after Task 8. If the build fails because of the missing folder, create an empty `web/Components` folder first: `New-Item -ItemType Directory -Path web/Components`.)

---

## Task 8: Build `Cell.razor` and `BoardView.razor`

**Files:**
- Create: `./web/Components/Cell.razor`
- Create: `./web/Components/BoardView.razor`

- [ ] **Step 1: Ensure the directory exists**

```powershell
New-Item -ItemType Directory -Path web/Components -Force
```

- [ ] **Step 2: Write `web/Components/Cell.razor`**

```razor
@using TicTacToe.Core

<div class="cell @CellClass()" @onclick="HandleClick">
    @if (Content != Player.None)
    {
        <div class="sigil sigil-@FactionClass(Content) @(IsHighlighted ? "won" : "")">
            <span class="sigil-glyph">@ThemeText.SymbolOf(Content)</span>
        </div>
    }
    else if (HoverPlayer != Player.None && IsHoverEnabled)
    {
        <div class="sigil sigil-@FactionClass(HoverPlayer) sigil-ghost">
            <span class="sigil-glyph">@ThemeText.SymbolOf(HoverPlayer)</span>
        </div>
    }
</div>

@code {
    [Parameter] public Player Content { get; set; } = Player.None;
    [Parameter] public Player HoverPlayer { get; set; } = Player.None;
    [Parameter] public bool IsHoverEnabled { get; set; }
    [Parameter] public bool IsHighlighted { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }

    private string CellClass()
    {
        var classes = "";
        if (Content == Player.None && IsHoverEnabled) classes += " cell-empty";
        if (IsHighlighted) classes += " cell-won";
        return classes.Trim();
    }

    private static string FactionClass(Player p) => p switch
    {
        Player.Ascendant => "ascendant",
        Player.Vanguard  => "vanguard",
        _                => "none"
    };

    private async Task HandleClick()
    {
        if (Content == Player.None && IsHoverEnabled)
            await OnClick.InvokeAsync();
    }
}
```

- [ ] **Step 3: Write `web/Components/BoardView.razor`**

```razor
@using TicTacToe.Core

<div class="board-grid">
    @for (int r = 0; r < 3; r++)
    {
        @for (int c = 0; c < 3; c++)
        {
            var row = r;
            var col = c;
            <Cell Content="@Board.At(row, col)"
                  HoverPlayer="@HoverPlayer"
                  IsHoverEnabled="@(IsCellInteractive(row, col))"
                  IsHighlighted="@IsWinningCell(row, col)"
                  OnClick="@(() => OnCellClick.InvokeAsync((row, col)))" />
        }
    }
</div>

@code {
    [Parameter, EditorRequired] public Board Board { get; set; } = default!;
    [Parameter] public Player HoverPlayer { get; set; } = Player.None;
    [Parameter] public bool AcceptingClicks { get; set; } = true;
    [Parameter] public (int row, int col)[]? WinningCells { get; set; }
    [Parameter] public EventCallback<(int row, int col)> OnCellClick { get; set; }

    private bool IsCellInteractive(int row, int col)
        => AcceptingClicks && Board.At(row, col) == Player.None && WinningCells is null;

    private bool IsWinningCell(int row, int col)
    {
        if (WinningCells is null) return false;
        foreach (var (wr, wc) in WinningCells)
            if (wr == row && wc == col) return true;
        return false;
    }
}
```

- [ ] **Step 4: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: builds cleanly. The components don't render anything yet because nothing references them — Task 11 wires them in.

---

## Task 9: Build `MainMenu.razor`, `ModeSetup.razor`, `ScoreBoard.razor`

**Files:**
- Create: `./web/Components/MainMenu.razor`
- Create: `./web/Components/ModeSetup.razor`
- Create: `./web/Components/ScoreBoard.razor`

- [ ] **Step 1: Write `MainMenu.razor`**

```razor
<div class="menu-panel">
    <h1 class="title">Order vs Rebellion</h1>
    <p class="tagline">A duel of cosmic sigils.</p>
    <div class="menu-buttons">
        <button class="btn btn-primary" @onclick="OnHotSeat">Hot-seat (two humans)</button>
        <button class="btn btn-primary" @onclick="OnVsComputer">Versus the computer</button>
    </div>
</div>

@code {
    [Parameter] public EventCallback OnHotSeat { get; set; }
    [Parameter] public EventCallback OnVsComputer { get; set; }
}
```

- [ ] **Step 2: Write `ModeSetup.razor`**

```razor
@using TicTacToe.Core

<div class="menu-panel">
    <h2 class="subtitle">Choose your faction</h2>
    <div class="faction-row">
        <button class="faction-card faction-ascendant @(SelectedFaction == Player.Ascendant ? "selected" : "")"
                @onclick="@(() => SelectedFaction = Player.Ascendant)">
            <div class="sigil sigil-ascendant"><span class="sigil-glyph">@ThemeText.Sun</span></div>
            <div class="faction-name">@ThemeText.AscendantName</div>
            <div class="faction-note">Moves first</div>
        </button>
        <button class="faction-card faction-vanguard @(SelectedFaction == Player.Vanguard ? "selected" : "")"
                @onclick="@(() => SelectedFaction = Player.Vanguard)">
            <div class="sigil sigil-vanguard"><span class="sigil-glyph">@ThemeText.Sparkle</span></div>
            <div class="faction-name">@ThemeText.VanguardName</div>
            <div class="faction-note">Moves second</div>
        </button>
    </div>

    <h2 class="subtitle">Choose opponent difficulty</h2>
    <div class="difficulty-row">
        @foreach (var d in new[] { Difficulty.Recruit, Difficulty.Captain, Difficulty.Warlord })
        {
            var capture = d;
            <button class="difficulty-card @(SelectedDifficulty == capture ? "selected" : "")"
                    @onclick="@(() => SelectedDifficulty = capture)">
                <div class="difficulty-name">@ThemeText.DifficultyName(capture)</div>
                <div class="difficulty-note">@DifficultyNote(capture)</div>
            </button>
        }
    </div>

    <div class="menu-buttons">
        <button class="btn btn-secondary" @onclick="OnBack">Back</button>
        <button class="btn btn-primary" @onclick="HandleStart">Begin duel</button>
    </div>
</div>

@code {
    [Parameter] public EventCallback OnBack { get; set; }
    [Parameter] public EventCallback<(Player faction, Difficulty difficulty)> OnStart { get; set; }

    private Player SelectedFaction = Player.Ascendant;
    private Difficulty SelectedDifficulty = Difficulty.Captain;

    private static string DifficultyNote(Difficulty d) => d switch
    {
        Difficulty.Recruit => "Random — beatable warm-up.",
        Difficulty.Captain => "Tactical — wins or blocks one move ahead.",
        Difficulty.Warlord => "Unbeatable — perfect play.",
        _                  => ""
    };

    private async Task HandleStart()
        => await OnStart.InvokeAsync((SelectedFaction, SelectedDifficulty));
}
```

- [ ] **Step 3: Write `ScoreBoard.razor`**

```razor
@using TicTacToe.Core

<div class="score-board">
    <div class="score score-ascendant">
        <span class="score-sigil">@ThemeText.Sun</span>
        <span class="score-name">@ThemeText.AscendantName</span>
        <span class="score-value">@AscendantWins</span>
    </div>
    <div class="score score-draws">
        <span class="score-name">Stalemates</span>
        <span class="score-value">@Draws</span>
    </div>
    <div class="score score-vanguard">
        <span class="score-sigil">@ThemeText.Sparkle</span>
        <span class="score-name">@ThemeText.VanguardName</span>
        <span class="score-value">@VanguardWins</span>
    </div>
    @if (ShowReset)
    {
        <button class="btn btn-link" @onclick="OnReset">Reset score</button>
    }
</div>

@code {
    [Parameter] public int AscendantWins { get; set; }
    [Parameter] public int VanguardWins { get; set; }
    [Parameter] public int Draws { get; set; }
    [Parameter] public bool ShowReset { get; set; }
    [Parameter] public EventCallback OnReset { get; set; }
}
```

- [ ] **Step 4: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: builds cleanly.

---

## Task 10: Build `Game.razor` (root component, state machine)

**Files:**
- Create: `./web/Components/Game.razor`

- [ ] **Step 1: Write the root component**

```razor
@page "/"
@using TicTacToe.Core
@inject GameSession Session
@implements IDisposable

<div class="app-shell">
    <main class="app-main">
        @switch (Session.Phase)
        {
            case GamePhase.MainMenu:
                <MainMenu OnHotSeat="StartHotSeat"
                          OnVsComputer="GoToModeSetup" />
                break;

            case GamePhase.ModeSetup:
                <ModeSetup OnBack="GoToMainMenu"
                           OnStart="StartVersusComputer" />
                break;

            case GamePhase.InMatch:
            case GamePhase.MatchOver:
                <div class="match-panel">
                    <div class="status-banner">
                        @if (Session.Phase == GamePhase.MatchOver)
                        {
                            @if (Session.Winner is { } w)
                            {
                                <span class="status-text status-@FactionClass(w)">@ThemeText.WinMessage(w)</span>
                            }
                            else if (Session.IsDraw)
                            {
                                <span class="status-text">@ThemeText.DrawMessage()</span>
                            }
                        }
                        else if (Session.AiThinking)
                        {
                            <span class="status-text status-@FactionClass(Session.Active) status-thinking">
                                @ThemeText.NameOf(Session.Active) considers the grid…
                            </span>
                        }
                        else
                        {
                            <span class="status-text status-@FactionClass(Session.Active)">
                                @ThemeText.NameOf(Session.Active) — claim a sector
                            </span>
                        }
                    </div>

                    <BoardView Board="Session.Board"
                               HoverPlayer="HoverPlayer"
                               AcceptingClicks="@(Session.Phase == GamePhase.InMatch && !Session.AiThinking && (!Session.VersusComputer || Session.Active == Session.HumanSide))"
                               WinningCells="Session.WinningCells"
                               OnCellClick="OnCellClick" />

                    <ScoreBoard AscendantWins="Session.AscendantWins"
                                VanguardWins="Session.VanguardWins"
                                Draws="Session.Draws"
                                ShowReset="true"
                                OnReset="ResetScore" />

                    @if (Session.Phase == GamePhase.MatchOver)
                    {
                        <div class="menu-buttons">
                            <button class="btn btn-primary" @onclick="PlayAgain">Play again</button>
                            <button class="btn btn-secondary" @onclick="GoToMainMenu">Main menu</button>
                        </div>
                    }
                </div>
                break;
        }
    </main>
</div>

@code {
    private Player HoverPlayer => Session.VersusComputer ? Session.HumanSide : Session.Active;

    protected override async Task OnInitializedAsync()
    {
        Session.OnChange += HandleStateChange;
        await Session.LoadScoresAsync();
    }

    private void HandleStateChange() => InvokeAsync(StateHasChanged);

    public void Dispose() => Session.OnChange -= HandleStateChange;

    private void StartHotSeat() => Session.StartHotSeat();
    private void GoToModeSetup() => Session.GoToModeSetup();
    private void GoToMainMenu() => Session.GoToMainMenu();

    private async Task StartVersusComputer((Player faction, Difficulty difficulty) args)
        => await Session.StartVersusComputerAsync(args.faction, args.difficulty);

    private async Task OnCellClick((int row, int col) move)
        => await Session.TryHumanMoveAsync(move.row, move.col);

    private async Task PlayAgain() => await Session.PlayAgainAsync();

    private async Task ResetScore() => await Session.ResetScoresAsync();

    private static string FactionClass(Player p) => p switch
    {
        Player.Ascendant => "ascendant",
        Player.Vanguard  => "vanguard",
        _                => "none"
    };
}
```

- [ ] **Step 2: Replace any leftover Pages stub**

If `web/Pages/Home.razor` (or `Index.razor`) still exists from Task 5, delete it. The `Game.razor` above has `@page "/"` and is the new root.

```powershell
Remove-Item web/Pages/Home.razor -ErrorAction SilentlyContinue
Remove-Item web/Pages/Index.razor -ErrorAction SilentlyContinue
```

- [ ] **Step 3: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: builds cleanly. **The page will render, but unstyled, until Task 11+ create the CSS.**

---

## Task 11: Write `theme.css` (palette, typography, asset slots, body backdrop)

**Files:**
- Create: `./web/wwwroot/css/theme.css`

- [ ] **Step 1: Create the css directory**

```powershell
New-Item -ItemType Directory -Path web/wwwroot/css -Force
```

- [ ] **Step 2: Write `theme.css`**

```css
:root {
    /* Background layers */
    --bg-deep:   #0a0518;
    --bg-mid:    #1a1530;
    --bg-nebula: #2a1a4a;

    /* Faction colors */
    --color-gold:         #ffd76b;
    --color-gold-glow:    rgba(255, 200, 80, 0.85);
    --color-crimson:      #ff7a9e;
    --color-crimson-glow: rgba(255, 80, 140, 0.85);

    /* UI text */
    --text-primary:   #e8e2ff;
    --text-secondary: #b8b0d8;
    --text-mute:      #6a6088;

    /* Typography */
    --font-serif: 'Spectral', 'Cormorant Garamond', Georgia, serif;
    --font-ui:    'Inter', system-ui, -apple-system, sans-serif;

    /* PNG asset slots — Phase 2 swaps these */
    --asset-ascendant-sigil: url('/img/ascendant-sigil.svg');
    --asset-vanguard-sigil:  url('/img/vanguard-sigil.svg');
    --asset-backdrop:        none;
}

html, body {
    margin: 0;
    padding: 0;
    min-height: 100vh;
    font-family: var(--font-ui);
    color: var(--text-primary);
    background: var(--bg-deep);
}

body {
    background:
        radial-gradient(1px 1px at 12% 18%, #fff 50%, transparent 51%),
        radial-gradient(1px 1px at 28% 64%, #fff 50%, transparent 51%),
        radial-gradient(1px 1px at 45% 32%, #ffeec7 50%, transparent 51%),
        radial-gradient(2px 2px at 62% 78%, #fff 50%, transparent 51%),
        radial-gradient(1px 1px at 78% 22%, #cad9ff 50%, transparent 51%),
        radial-gradient(1px 1px at 88% 70%, #fff 50%, transparent 51%),
        radial-gradient(1px 1px at 8% 88%, #ffeec7 50%, transparent 51%),
        radial-gradient(1px 1px at 35% 8%, #fff 50%, transparent 51%),
        radial-gradient(2px 2px at 70% 48%, #fff 50%, transparent 51%),
        radial-gradient(ellipse 60% 40% at 30% 70%, var(--bg-nebula), transparent 70%),
        radial-gradient(ellipse 50% 35% at 75% 30%, var(--bg-mid), transparent 70%),
        radial-gradient(ellipse at center, var(--bg-mid) 0%, var(--bg-deep) 70%);
    background-attachment: fixed;
}

/* When --asset-backdrop is overridden to a url(), this layer becomes visible. */
body::before {
    content: '';
    position: fixed;
    inset: 0;
    background-image: var(--asset-backdrop);
    background-size: cover;
    background-position: center;
    opacity: 0.6;
    pointer-events: none;
    z-index: -1;
}

#app, .app-shell {
    min-height: 100vh;
    display: flex;
    flex-direction: column;
}

.app-main {
    flex: 1;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 24px;
}

/* Headings */
.title, .subtitle, h1, h2 {
    font-family: var(--font-serif);
    font-weight: 600;
    color: var(--text-primary);
    margin: 0 0 16px;
}
.title {
    font-size: clamp(28px, 5vw, 48px);
    letter-spacing: 0.02em;
    text-align: center;
}
.tagline {
    font-family: var(--font-serif);
    font-style: italic;
    color: var(--text-secondary);
    text-align: center;
    margin: 0 0 32px;
    font-size: clamp(14px, 2vw, 18px);
}
.subtitle {
    font-size: clamp(18px, 3vw, 24px);
    text-align: center;
}

/* Buttons */
.btn {
    border: 1px solid rgba(232, 226, 255, 0.2);
    background: rgba(255, 255, 255, 0.04);
    color: var(--text-primary);
    font-family: var(--font-ui);
    font-size: 16px;
    padding: 12px 24px;
    border-radius: 4px;
    cursor: pointer;
    min-height: 48px;
    transition: background 0.15s, border-color 0.15s, transform 0.1s;
}
.btn:hover { background: rgba(255, 255, 255, 0.08); border-color: rgba(232, 226, 255, 0.4); }
.btn:active { transform: translateY(1px); }
.btn-primary { border-color: var(--color-gold); }
.btn-primary:hover { background: rgba(255, 215, 107, 0.1); }
.btn-secondary { color: var(--text-secondary); }
.btn-link {
    background: none; border: none; color: var(--text-mute);
    font-size: 12px; min-height: auto; padding: 4px 8px;
    text-transform: uppercase; letter-spacing: 0.08em;
}
.btn-link:hover { color: var(--text-secondary); }

.menu-panel {
    max-width: 600px;
    width: 100%;
    text-align: center;
}
.menu-buttons {
    display: flex; gap: 12px; justify-content: center; flex-wrap: wrap;
    margin-top: 16px;
}

.faction-row, .difficulty-row {
    display: grid;
    gap: 12px;
    margin: 16px 0 32px;
}
.faction-row { grid-template-columns: 1fr 1fr; }
.difficulty-row { grid-template-columns: repeat(3, 1fr); }

@media (max-width: 480px) {
    .faction-row, .difficulty-row { grid-template-columns: 1fr; }
    .menu-buttons { flex-direction: column; }
    .menu-buttons .btn { width: 100%; }
}

.faction-card, .difficulty-card {
    background: rgba(255, 255, 255, 0.03);
    border: 1px solid rgba(232, 226, 255, 0.15);
    border-radius: 4px;
    padding: 16px;
    color: var(--text-primary);
    cursor: pointer;
    transition: border-color 0.15s, background 0.15s;
    font-family: var(--font-ui);
    text-align: center;
}
.faction-card.selected { border-color: var(--color-gold); background: rgba(255, 215, 107, 0.06); }
.faction-card.faction-vanguard.selected { border-color: var(--color-crimson); background: rgba(255, 122, 158, 0.06); }
.difficulty-card.selected { border-color: var(--color-gold); background: rgba(255, 215, 107, 0.06); }
.faction-name, .difficulty-name {
    font-family: var(--font-serif);
    font-size: 18px;
    margin: 8px 0 4px;
}
.faction-note, .difficulty-note {
    font-size: 12px;
    color: var(--text-mute);
}
```

- [ ] **Step 3: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: builds cleanly.

---

## Task 12: Write `board.css` (grid + cell layout + win highlight)

**Files:**
- Create: `./web/wwwroot/css/board.css`

- [ ] **Step 1: Write `board.css`**

```css
.match-panel {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 24px;
    width: 100%;
    max-width: 540px;
}

.status-banner {
    min-height: 32px;
    text-align: center;
    font-family: var(--font-serif);
    font-size: clamp(16px, 2.5vw, 20px);
    color: var(--text-secondary);
}
.status-text.status-ascendant { color: var(--color-gold); }
.status-text.status-vanguard { color: var(--color-crimson); }
.status-thinking { font-style: italic; opacity: 0.85; }

.board-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    grid-template-rows: repeat(3, 1fr);
    gap: 8px;
    aspect-ratio: 1 / 1;
    width: min(60vmin, 480px);
    padding: 8px;
    background: rgba(255, 255, 255, 0.02);
    border: 1px solid rgba(232, 226, 255, 0.08);
    border-radius: 6px;
}

.cell {
    background: rgba(255, 255, 255, 0.02);
    border: 1px solid rgba(232, 226, 255, 0.06);
    border-radius: 4px;
    display: flex;
    align-items: center;
    justify-content: center;
    aspect-ratio: 1 / 1;
    overflow: hidden;
    position: relative;
    transition: border-color 0.15s, background 0.15s;
}
.cell-empty { cursor: pointer; }
.cell-empty:hover { background: rgba(255, 255, 255, 0.05); border-color: rgba(232, 226, 255, 0.18); }

.sigil {
    width: 70%;
    height: 70%;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: clamp(40px, 11vmin, 100px);
    line-height: 1;
}
.sigil-glyph { display: inline-block; }

.sigil-ascendant {
    color: var(--color-gold);
    filter: drop-shadow(0 0 12px var(--color-gold-glow))
            drop-shadow(0 0 24px rgba(255, 200, 80, 0.4));
}
.sigil-vanguard {
    color: var(--color-crimson);
    filter: drop-shadow(0 0 12px var(--color-crimson-glow))
            drop-shadow(0 0 24px rgba(255, 80, 140, 0.4));
}
.sigil-ghost { opacity: 0.25; transition: opacity 0.15s; }

.cell-won { border-color: rgba(232, 226, 255, 0.4); }
.cell-won.cell:has(.sigil-ascendant) { border-color: var(--color-gold); box-shadow: inset 0 0 24px rgba(255, 215, 107, 0.2); }
.cell-won.cell:has(.sigil-vanguard) { border-color: var(--color-crimson); box-shadow: inset 0 0 24px rgba(255, 122, 158, 0.2); }

.score-board {
    display: flex;
    align-items: center;
    justify-content: space-between;
    width: min(60vmin, 480px);
    padding: 12px 16px;
    background: rgba(255, 255, 255, 0.02);
    border: 1px solid rgba(232, 226, 255, 0.08);
    border-radius: 4px;
    flex-wrap: wrap;
    gap: 12px;
}
.score { display: flex; flex-direction: column; align-items: center; min-width: 80px; }
.score-sigil { font-size: 20px; line-height: 1; }
.score-ascendant .score-sigil { color: var(--color-gold); }
.score-vanguard .score-sigil { color: var(--color-crimson); }
.score-name {
    font-size: 11px;
    color: var(--text-mute);
    text-transform: uppercase;
    letter-spacing: 0.08em;
    margin-top: 4px;
}
.score-value {
    font-family: var(--font-serif);
    font-size: 24px;
    color: var(--text-primary);
    margin-top: 2px;
}

@media (max-width: 480px) {
    .score-board { padding: 8px 12px; gap: 8px; }
    .score { min-width: 60px; }
    .score-value { font-size: 18px; }
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: builds cleanly.

---

## Task 13: Write `animations.css` (placement, idle pulse, win-line)

**Files:**
- Create: `./web/wwwroot/css/animations.css`

- [ ] **Step 1: Write `animations.css`**

```css
/* ── Placement bloom — when a sigil first appears ─────────────────────── */
@keyframes placement-bloom {
    0%   { transform: scale(0.5); opacity: 0; filter: drop-shadow(0 0 0 transparent); }
    60%  { transform: scale(1.1); opacity: 1; filter: drop-shadow(0 0 32px currentColor); }
    100% { transform: scale(1.0); opacity: 1; }
}

/* ── Idle pulse — placed sigils breathe gently ────────────────────────── */
@keyframes idle-pulse-gold {
    0%, 100% { filter: drop-shadow(0 0 12px var(--color-gold-glow)) drop-shadow(0 0 24px rgba(255, 200, 80, 0.4)); opacity: 0.92; }
    50%      { filter: drop-shadow(0 0 18px var(--color-gold-glow)) drop-shadow(0 0 36px rgba(255, 200, 80, 0.55)); opacity: 1; }
}
@keyframes idle-pulse-crimson {
    0%, 100% { filter: drop-shadow(0 0 12px var(--color-crimson-glow)) drop-shadow(0 0 24px rgba(255, 80, 140, 0.4)); opacity: 0.92; }
    50%      { filter: drop-shadow(0 0 18px var(--color-crimson-glow)) drop-shadow(0 0 36px rgba(255, 80, 140, 0.55)); opacity: 1; }
}

.sigil-ascendant:not(.sigil-ghost) {
    animation: placement-bloom 0.4s cubic-bezier(0.2, 0.8, 0.3, 1.4) backwards,
               idle-pulse-gold 4.5s ease-in-out infinite;
    animation-delay: 0s, 0.4s;
}
.sigil-vanguard:not(.sigil-ghost) {
    animation: placement-bloom 0.4s cubic-bezier(0.2, 0.8, 0.3, 1.4) backwards,
               idle-pulse-crimson 4.5s ease-in-out infinite;
    animation-delay: 0s, 0.4s;
}

/* ── Hover ghost — fade in over 0.15s ─────────────────────────────────── */
.sigil-ghost { animation: ghost-fade-in 0.15s ease-out backwards; }
@keyframes ghost-fade-in {
    from { opacity: 0; }
    to   { opacity: 0.25; }
}

/* ── Winning cell — emphasize the three ───────────────────────────────── */
@keyframes win-cell-glow {
    0%   { box-shadow: inset 0 0 0 transparent; }
    100% { box-shadow: inset 0 0 32px currentColor; }
}
.cell-won {
    animation: win-cell-glow 0.6s ease-out forwards;
}

/* ── Status banner — gentle drop-in for new state ─────────────────────── */
.status-text {
    animation: status-drop 0.3s ease-out backwards;
}
@keyframes status-drop {
    from { transform: translateY(-4px); opacity: 0; }
    to   { transform: translateY(0);     opacity: 1; }
}

/* ── Reduced motion — respect user pref ───────────────────────────────── */
@media (prefers-reduced-motion: reduce) {
    .sigil-ascendant:not(.sigil-ghost), .sigil-vanguard:not(.sigil-ghost),
    .cell-won, .status-text, .sigil-ghost {
        animation: none !important;
    }
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: builds cleanly.

---

## Task 14: Write placeholder SVG sigils and asset README

**Files:**
- Create: `./web/wwwroot/img/ascendant-sigil.svg`
- Create: `./web/wwwroot/img/vanguard-sigil.svg`
- Create: `./web/wwwroot/img/README.md`

These SVG files act as the Phase-1 art. The Razor components currently render the unicode glyphs (☼ / ✦) directly via `ThemeText.SymbolOf(...)`, which already works without the SVGs. The SVG files exist so the `--asset-ascendant-sigil` / `--asset-vanguard-sigil` CSS custom properties can be used by future styles (e.g., a future `background-image` based sigil), and so the Phase 2 PNG swap has a 1:1 file-name correspondence.

- [ ] **Step 1: Create the img directory**

```powershell
New-Item -ItemType Directory -Path web/wwwroot/img -Force
```

- [ ] **Step 2: Write `ascendant-sigil.svg`**

```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">
  <defs>
    <radialGradient id="goldGlow" cx="50%" cy="50%" r="50%">
      <stop offset="0%" stop-color="#fff5d4"/>
      <stop offset="40%" stop-color="#ffd76b"/>
      <stop offset="100%" stop-color="#c08a00"/>
    </radialGradient>
    <filter id="goldBloom" x="-50%" y="-50%" width="200%" height="200%">
      <feGaussianBlur stdDeviation="6" result="blur"/>
      <feMerge>
        <feMergeNode in="blur"/>
        <feMergeNode in="SourceGraphic"/>
      </feMerge>
    </filter>
  </defs>
  <g filter="url(#goldBloom)" fill="url(#goldGlow)">
    <!-- 12 rays -->
    <g transform="translate(128 128)">
      <g>
        <path d="M0 -100 L8 -50 L-8 -50 Z"/>
      </g>
      <g transform="rotate(30)">
        <path d="M0 -100 L8 -50 L-8 -50 Z"/>
      </g>
      <g transform="rotate(60)">
        <path d="M0 -100 L8 -50 L-8 -50 Z"/>
      </g>
      <g transform="rotate(90)">
        <path d="M0 -100 L8 -50 L-8 -50 Z"/>
      </g>
      <g transform="rotate(120)">
        <path d="M0 -100 L8 -50 L-8 -50 Z"/>
      </g>
      <g transform="rotate(150)">
        <path d="M0 -100 L8 -50 L-8 -50 Z"/>
      </g>
      <g transform="rotate(180)">
        <path d="M0 -100 L8 -50 L-8 -50 Z"/>
      </g>
      <g transform="rotate(210)">
        <path d="M0 -100 L8 -50 L-8 -50 Z"/>
      </g>
      <g transform="rotate(240)">
        <path d="M0 -100 L8 -50 L-8 -50 Z"/>
      </g>
      <g transform="rotate(270)">
        <path d="M0 -100 L8 -50 L-8 -50 Z"/>
      </g>
      <g transform="rotate(300)">
        <path d="M0 -100 L8 -50 L-8 -50 Z"/>
      </g>
      <g transform="rotate(330)">
        <path d="M0 -100 L8 -50 L-8 -50 Z"/>
      </g>
    </g>
    <!-- core disc -->
    <circle cx="128" cy="128" r="36"/>
  </g>
</svg>
```

- [ ] **Step 3: Write `vanguard-sigil.svg`**

```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">
  <defs>
    <radialGradient id="crimsonGlow" cx="50%" cy="50%" r="50%">
      <stop offset="0%" stop-color="#ffd0e0"/>
      <stop offset="40%" stop-color="#ff7a9e"/>
      <stop offset="100%" stop-color="#9b1d3d"/>
    </radialGradient>
    <filter id="crimsonBloom" x="-50%" y="-50%" width="200%" height="200%">
      <feGaussianBlur stdDeviation="6" result="blur"/>
      <feMerge>
        <feMergeNode in="blur"/>
        <feMergeNode in="SourceGraphic"/>
      </feMerge>
    </filter>
  </defs>
  <g filter="url(#crimsonBloom)" fill="url(#crimsonGlow)">
    <g transform="translate(128 128)">
      <!-- 4 main points -->
      <path d="M0 -100 L20 -10 L0 0 L-20 -10 Z"/>
      <path d="M100 0 L10 20 L0 0 L10 -20 Z"/>
      <path d="M0 100 L-20 10 L0 0 L20 10 Z"/>
      <path d="M-100 0 L-10 -20 L0 0 L-10 20 Z"/>
      <!-- 4 diagonal accents -->
      <g opacity="0.7">
        <path d="M55 -55 L8 -8 L0 0 L-8 8 L55 -55Z" transform="rotate(45)"/>
        <path d="M55 -55 L8 -8 L0 0 L-8 8 L55 -55Z" transform="rotate(135)"/>
        <path d="M55 -55 L8 -8 L0 0 L-8 8 L55 -55Z" transform="rotate(225)"/>
        <path d="M55 -55 L8 -8 L0 0 L-8 8 L55 -55Z" transform="rotate(315)"/>
      </g>
    </g>
    <circle cx="128" cy="128" r="14"/>
  </g>
</svg>
```

- [ ] **Step 4: Write `web/wwwroot/img/README.md`**

```markdown
# Visual Assets — Phase 2 PNG Swap

Drop PNGs into this folder matching the slot names below, then update the
matching `--asset-*` lines in `web/wwwroot/css/theme.css`. No code changes.

| Slot                      | Phase 1 default     | Phase 2 file expected            | Min size                | Notes                         |
|---------------------------|---------------------|----------------------------------|-------------------------|-------------------------------|
| Ascendant sigil           | `ascendant-sigil.svg` (sun) | `ascendant-sigil.png`    | 256×256, transparent    | Round bounding circle ideal   |
| Vanguard sigil            | `vanguard-sigil.svg` (sparkle) | `vanguard-sigil.png`  | 256×256, transparent    | Round bounding circle ideal   |
| Backdrop                  | (none — pure CSS)   | `backdrop.png` or `.jpg`         | 1920×1080               | Optional; covers full viewport at 60% opacity |
| App icon (192px)          | placeholder         | `icons/icon-192.png`             | 192×192                 | Solid background OK           |
| App icon (512px)          | placeholder         | `icons/icon-512.png`             | 512×512                 | Used for splash on Android    |
| Maskable 192              | placeholder         | `icons/icon-maskable-192.png`    | 192×192                 | 80% safe area inside          |
| Maskable 512              | placeholder         | `icons/icon-maskable-512.png`    | 512×512                 | 80% safe area inside          |
| Apple touch icon          | placeholder         | `icons/apple-touch-icon.png`     | 180×180                 | No transparency               |
| Favicon                   | inline SVG          | `favicon.ico` (already in wwwroot root) | 32×32 ICO         | Multiple sizes optional       |

After swapping, edit `web/wwwroot/css/theme.css`:

```css
:root {
    --asset-ascendant-sigil: url('/img/ascendant-sigil.png');
    --asset-vanguard-sigil:  url('/img/vanguard-sigil.png');
    --asset-backdrop:        url('/img/backdrop.png');  /* enables the body::before layer */
}
```

The `body::before` layer in `theme.css` paints the backdrop image at 60% opacity behind the CSS starfield — the starfield stays visible on top.
```

- [ ] **Step 5: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: builds cleanly.

---

## Task 15: Generate placeholder app icons

The PWA template generates default Blazor icons; we need to either replace them with cosmic-themed placeholders or commit to the templates. Phase 1 acceptance: simple solid-color squares with the sigil glyph, generated programmatically so we don't ship copied art.

**Files:**
- Create: `./web/wwwroot/img/icons/icon-192.png`
- Create: `./web/wwwroot/img/icons/icon-512.png`
- Create: `./web/wwwroot/img/icons/icon-maskable-192.png`
- Create: `./web/wwwroot/img/icons/icon-maskable-512.png`
- Create: `./web/wwwroot/img/icons/apple-touch-icon.png`
- Create: `./web/wwwroot/favicon.ico` (replace template's)

- [ ] **Step 1: Create the icons directory**

```powershell
New-Item -ItemType Directory -Path web/wwwroot/img/icons -Force
```

- [ ] **Step 2: Generate placeholder PNGs via a small one-off C# script**

Create a temporary file `web/icon-gen.csx` (deleted after use):

```csharp
#r "System.Drawing.Common"
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

void Generate(int size, string path, bool maskable)
{
    using var bmp = new Bitmap(size, size);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

    // Background — deep indigo
    using var bg = new SolidBrush(Color.FromArgb(0x0a, 0x05, 0x18));
    g.FillRectangle(bg, 0, 0, size, size);

    // For maskable, leave 10% safe area on each side; else fill more.
    var pad = maskable ? size / 5 : size / 10;

    // Subtle glow ring
    using var ringPen = new Pen(Color.FromArgb(40, 255, 215, 107), size * 0.02f);
    g.DrawEllipse(ringPen, pad, pad, size - 2*pad, size - 2*pad);

    // Sun glyph in gold — drawn as 12 rays + central disc
    var cx = size / 2.0f;
    var cy = size / 2.0f;
    var rOuter = (size - 2*pad) / 2.0f;
    var rInner = rOuter * 0.5f;
    using var goldBrush = new SolidBrush(Color.FromArgb(255, 215, 107));
    for (int i = 0; i < 12; i++)
    {
        var a = i * Math.PI / 6.0;
        var p1 = new PointF(
            cx + (float)Math.Cos(a - Math.PI/2) * rOuter,
            cy + (float)Math.Sin(a - Math.PI/2) * rOuter);
        var p2 = new PointF(
            cx + (float)Math.Cos(a + 0.13 - Math.PI/2) * rInner,
            cy + (float)Math.Sin(a + 0.13 - Math.PI/2) * rInner);
        var p3 = new PointF(
            cx + (float)Math.Cos(a - 0.13 - Math.PI/2) * rInner,
            cy + (float)Math.Sin(a - 0.13 - Math.PI/2) * rInner);
        g.FillPolygon(goldBrush, new[] { p1, p2, p3 });
    }
    g.FillEllipse(goldBrush, cx - rInner * 0.5f, cy - rInner * 0.5f, rInner, rInner);

    bmp.Save(path, ImageFormat.Png);
    Console.WriteLine($"wrote {path}");
}

Generate(192, "wwwroot/img/icons/icon-192.png", maskable: false);
Generate(512, "wwwroot/img/icons/icon-512.png", maskable: false);
Generate(192, "wwwroot/img/icons/icon-maskable-192.png", maskable: true);
Generate(512, "wwwroot/img/icons/icon-maskable-512.png", maskable: true);
Generate(180, "wwwroot/img/icons/apple-touch-icon.png", maskable: false);
Generate(32, "wwwroot/favicon.png", maskable: false);
```

- [ ] **Step 3: Run the script**

```powershell
cd web
dotnet script icon-gen.csx
cd ..
```

If `dotnet script` is not installed, install it first:
```powershell
dotnet tool install -g dotnet-script
```
…then re-run Step 3. If `System.Drawing.Common` complains on Windows about libgdiplus or platform support, replace the script with the simpler fallback in Step 4 below.

**Fallback if dotnet-script unavailable:** ship solid-color SVGs and convert manually. Skip Step 3 and instead write each icon as a tiny SVG (via the same shape data above), then use any browser to "save as PNG" or use ImageMagick: `magick convert -background "#0a0518" -size 192x192 icon-192.svg icon-192.png`. For v1 acceptance, a pure-color square saved as a PNG (e.g., a single 192×192 image of `#0a0518` with the sun-rune drawn in gold) is sufficient — the PWA installs and looks intentional. Any PNG file at the right path and size makes the manifest validate.

- [ ] **Step 4: Convert `favicon.png` to `favicon.ico` (or accept PNG-as-ICO)**

Modern browsers accept PNG files renamed to `.ico`. Simplest approach:
```powershell
Move-Item -Force web/wwwroot/favicon.png web/wwwroot/favicon.ico
```

- [ ] **Step 5: Delete the icon generator script**

```powershell
Remove-Item web/icon-gen.csx
```

- [ ] **Step 6: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: builds cleanly.

---

## Task 16: First end-to-end run + manual smoke checklist

This is the v1 acceptance gate. All items must pass.

- [ ] **Step 1: Console parity — refactor regression check**

```powershell
cd console
pwsh -NoProfile -ExecutionPolicy Bypass -File tests/smoke.ps1
cd ..
```
Expected: `10 / 10 scenarios passed.`

- [ ] **Step 2: Web build is clean**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: `0 Warning(s). 0 Error(s).`

- [ ] **Step 3: Start the dev server**

```powershell
dotnet run --project web
```
Expected output includes `Now listening on: http://localhost:5xxx`. Open that URL in Chrome/Edge.

- [ ] **Step 4: Visual sanity**

In the browser:
- Title "Order vs Rebellion" reads in Spectral serif against a dark starfield backdrop.
- "Hot-seat (two humans)" and "Versus the computer" buttons appear.
- DevTools Console is empty (no 404s for CSS/SVGs/icons).

- [ ] **Step 5: Hot-seat row win**

Click `Hot-seat`. Click the cells in this order: top-left, top-middle (no — wait — alternating). Click A1 (top-left), A2 (middle-left), B1 (top-middle), B2 (center), C1 (top-right). Expected: top row glows gold, "The Ascendant holds the line — three sectors in a row." appears, score reads `Ascendant 1 — Stalemates 0 — Vanguard 0`.

- [ ] **Step 6: Hot-seat column + diagonal**

Click `Play again`, win column A for Ascendant. Verify column glow + score. Play again, win diagonal A1/B2/C3. Verify diagonal glow + score.

- [ ] **Step 7: Hot-seat draw**

Play a stalemate (use a known sequence: B2, A1, A3, C1, B1, B3, C3, C2, A2). Verify "Stalemate. The galaxy holds its breath." and stalemate count increments.

- [ ] **Step 8: Vs Recruit / Captain / Warlord**

Back to main menu → Versus the computer → choose Ascendant + Recruit. Play; AI moves arrive after ~600ms with a "considers the grid…" status. Win or lose; replay. Repeat with Captain (verify it blocks an obvious threat). Repeat with Warlord — play 3 games, verify the human never wins.

- [ ] **Step 9: Persistence**

Refresh the browser. Verify the score from the previous matches is still showing. Click `Reset score`. Verify the score zeros out. Refresh again — still zeros.

- [ ] **Step 10: Mobile size**

Open Chrome DevTools (F12), toggle device toolbar (Ctrl+Shift+M), pick iPhone SE (375×667). Reload. Verify:
- Title is readable and centered.
- Faction/difficulty cards stack vertically (one column).
- Board fills width minus padding, stays square.
- Buttons are at least 48px tall.
- Sigil glow is not cut off.

- [ ] **Step 11: PWA install (desktop)**

Back to full-window in Chrome/Edge. Look for the "Install app" button at the right edge of the URL bar (or three-dot menu → "Install Order vs Rebellion"). Install. Verify the app launches in a borderless window with the cosmic background. Play a match. Close the app.

- [ ] **Step 12: Offline play**

Open Chrome DevTools → Network tab → throttle dropdown → "Offline." Reload the page. Verify the game loads from the service-worker cache and is fully playable.

- [ ] **Step 13: Lighthouse audit**

DevTools → Lighthouse tab → uncheck Performance/Accessibility/Best Practices/SEO, leave PWA checked. Run. Expected: PWA score ≥ 90. If lower, read the failing checks and fix only the easy ones (icon sizes, manifest fields). Big drops typically mean a missing icon size or manifest field.

- [ ] **Step 14: Stop the dev server**

`Ctrl+C` in the terminal where `dotnet run` is running. Confirm it exits cleanly.

---

## Self-Review Checklist (run by the plan author after writing all tasks)

- [x] **Spec coverage:** Every spec section has a task. Project layout → Tasks 1, 5. Shared library → Tasks 1–3. Console refactor → Tasks 2–4. Web architecture (state machine, components, GameSession) → Tasks 7–10. Visual implementation (CSS files, animations, asset slots) → Tasks 11–14. PWA configuration → Tasks 6, 15. Persistence → Task 7 (built into `GameSession`). Manual checklist → Task 16.
- [x] **Placeholder scan:** No TBD/TODO. All code blocks contain real code. Step 4 of Task 15 has a documented fallback path, not a placeholder.
- [x] **Type consistency:** `GamePhase`, `GameSession`, `Player`, `Difficulty`, `Board`, `Ai`, `ThemeText`, `ConsolePalette` used identically in every task that references them. `Cell`/`BoardView`/`MainMenu`/`ModeSetup`/`ScoreBoard`/`Game` parameter names match between definitions and call sites.
- [x] **Build sequencing:** Tasks 7–10 may produce intermediate states with unstyled rendering, but every task ends with `dotnet build` clean. Task 11+ apply styling. Task 16 is the only task that requires a successful `dotnet run` and a real browser.

---

## Execution

Plan complete. Ready to execute when the user is.
