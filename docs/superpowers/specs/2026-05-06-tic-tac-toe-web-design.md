# Tic-Tac-Toe Web — Cosmic Sigils Edition

**Date:** 2026-05-06
**Author:** dderbidge
**Status:** Design (pre-implementation)
**Builds on:** `docs/superpowers/specs/2026-05-05-tic-tac-toe-design.md` (the original console version, now in `console/`)

## Overview

A Blazor WebAssembly version of the existing Order vs Rebellion tic-tac-toe game, presented as a "Cosmic Sigils" experience: dark starfield backdrop, glowing sun (☼) and sparkle (✦) sigils, soft pulse animations, mythic typography. Installable as a Progressive Web App on desktop and mobile, fully offline-capable after first load. Game logic (`Board`, `Ai`) is shared with the existing console version via a new `core/` class library — single source of truth, no logic duplication.

The web UI ships in two phases:
- **Phase 1 (this spec):** polished pure-CSS / inline-SVG placeholders that already look good. All visual asset slots are defined as CSS custom properties so swapping in artwork is a one-file change.
- **Phase 2 (later):** drop user-supplied PNGs into `web/wwwroot/img/`, update the `--asset-*` lines in `theme.css`. No code changes.

## Goals & Non-Goals

**Goals**
- A finished, installable, fully playable web tic-tac-toe game with the cosmic theme.
- Same gameplay parity with the console version: hot-seat, vs CPU at three difficulty tiers, in-session score, identical win/draw rules.
- Reuse the proven `Board` and `Ai` C# code with zero duplication.
- Persistent score across page reloads (localStorage).
- PWA: installable on desktop and mobile, plays offline after first load.
- Mobile-friendly: works on a 375×667 phone viewport with touch.
- Asset-swap-ready: a graphic designer (or future-you) can drop PNGs and the look upgrades without code changes.

**Non-Goals**
- Network multiplayer.
- Sound effects (browser autoplay policies + asset management = scope bloat).
- Themes other than Cosmic Sigils (the design picks one direction and commits).
- Server-side anything (the app is fully client-side; no backend).
- Deployment to a hosted URL (running locally via `dotnet run` is sufficient for v1; deployment is a later concern).
- Replays, move undo, match history beyond aggregate score.

## Project Layout

Three sibling folders under the repo root, each with its own `.csproj`. A solution file at the root ties them together.

```
tic-tac-toe-game/
├── TicTacToe.sln                                 NEW
├── core/                                         NEW
│   ├── TicTacToe.Core.csproj
│   ├── Player.cs
│   ├── Difficulty.cs
│   ├── Board.cs                                  (moved from console/)
│   ├── Ai.cs                                     (moved from console/)
│   └── ThemeText.cs                              (faction names, symbols, flavor strings)
│
├── console/                                      EXISTING — refactored to reference core/
│   ├── TicTacToe.Console.csproj                  (renamed from TicTacToe.csproj)
│   ├── Program.cs
│   ├── Renderer.cs
│   ├── ConsolePalette.cs                         (NEW — ANSI codes + UseColor flag, was in Theme.cs)
│   ├── tests/, MORNING.md, docs/                 (unchanged)
│   └── (Board.cs, Ai.cs, old Theme.cs DELETED — now in core/)
│
└── web/                                          NEW — Blazor WebAssembly + PWA
    ├── TicTacToe.Web.csproj
    ├── Program.cs
    ├── App.razor, _Imports.razor, MainLayout.razor
    ├── Components/
    │   ├── Game.razor                            (root, owns the state machine)
    │   ├── BoardView.razor                       (3×3 grid)
    │   ├── Cell.razor                            (single sector)
    │   ├── MainMenu.razor                        (mode pick)
    │   ├── ModeSetup.razor                       (faction + difficulty)
    │   └── ScoreBoard.razor                      (score panel + reset)
    ├── Services/
    │   └── GameSession.cs                        (state holder, AI delay, localStorage)
    └── wwwroot/
        ├── index.html
        ├── manifest.webmanifest
        ├── service-worker.js, service-worker.published.js   (Blazor PWA defaults)
        ├── css/
        │   ├── theme.css                         (palette, typography, ASSET SLOTS)
        │   ├── board.css                         (grid, cells, hover, win highlight)
        │   └── animations.css                    (placement, idle pulse, win trace)
        ├── img/
        │   ├── README.md                         (asset spec for Phase 2)
        │   ├── ascendant-sigil.svg               (placeholder — sun)
        │   ├── vanguard-sigil.svg                (placeholder — sparkle)
        │   ├── backdrop.svg                      (placeholder — optional)
        │   └── icons/
        │       ├── icon-192.png, icon-512.png
        │       ├── icon-maskable-192.png, icon-maskable-512.png
        │       └── apple-touch-icon.png
        └── favicon.ico
```

## Shared Library: `core/TicTacToe.Core`

A class library targeting `net9.0`, `Nullable: enable`, no NuGet dependencies. Contents:

| File | Source | Notes |
|---|---|---|
| `Player.cs` | extracted from current `Board.cs` | unchanged enum |
| `Difficulty.cs` | extracted from current `Ai.cs` | unchanged enum |
| `Board.cs` | moved verbatim from `console/Board.cs` | namespace becomes `TicTacToe.Core` |
| `Ai.cs` | moved verbatim from `console/Ai.cs` | namespace becomes `TicTacToe.Core` |
| `ThemeText.cs` | extracted from `console/Theme.cs` | platform-agnostic strings only |

`ThemeText.cs` contains:
- Constants: `AscendantName`, `VanguardName`, `Sun`, `Sparkle`
- Static helpers: `NameOf(Player)`, `SymbolOf(Player)`, `DifficultyName(Difficulty)`, `PlacementMessage(Player, sector)`, `WinMessage(Player)`, `DrawMessage()`, `OccupiedMessage(sector)`, `InvalidCoordMessage()`

The `UseColor` flag, ANSI escape constants, and the `Color(...)`, `ColoredSymbolOf(...)`, `ColoredNameOf(...)` helpers move into a new `console/ConsolePalette.cs` and stay console-only.

## Console Refactor

Existing console smoke tests (10 scenarios in `tests/smoke.ps1`) MUST continue to pass byte-for-byte after the refactor. Steps:

1. Create `core/TicTacToe.Core.csproj` (new class library).
2. Move `Board.cs` and `Ai.cs` from `console/` to `core/` and update namespaces to `TicTacToe.Core`.
3. Split `console/Theme.cs`: platform-agnostic bits → `core/ThemeText.cs`; ANSI bits → `console/ConsolePalette.cs`.
4. In `console/`, add `<ProjectReference Include="..\core\TicTacToe.Core.csproj" />` and `using TicTacToe.Core;` to files that need it.
5. Rename `console/TicTacToe.csproj` → `console/TicTacToe.Console.csproj` (and update root namespace if needed).
6. Update `console/Renderer.cs` to call `ConsolePalette.Color(...)`, `ConsolePalette.ColoredSymbolOf(...)`, etc., where it currently calls `Theme.*`.
7. Run `dotnet build` from repo root via solution file; run `tests/smoke.ps1` from `console/`; expect 10/10 pass.

## Web App Architecture

### State machine

Owned by `Components/Game.razor`. The current state determines which child component renders.

```
                        ┌──────────────────────────────────┐
                        │           MainMenu               │
                        │  • Hot-seat                      │
                        │  • Vs Computer                   │
                        └──┬─────────────────────────────┬─┘
                           │                             │
                           │ "Hot-seat"                  │ "Vs Computer"
                           │                             ▼
                           │                       ┌────────────┐
                           │                       │ ModeSetup  │
                           │                       │ faction +  │
                           │                       │ difficulty │
                           │                       └─────┬──────┘
                           │                             │
                           ▼                             ▼
                        ┌──────────────────────────────────┐
                        │           InMatch                │
                        │  • BoardView (clickable)         │
                        │  • ScoreBoard                    │
                        │  • Active player banner          │
                        │  • "AI considering…" overlay     │
                        └─────────────────┬────────────────┘
                                          │
                                          ▼
                        ┌──────────────────────────────────┐
                        │          MatchOver               │
                        │  • BoardView (winning cells lit) │
                        │  • Result banner                 │
                        │  • Play again? / Main menu       │
                        └──┬─────────────────────────────┬─┘
                           │                             │
                           │ "Play again"                │ "Main menu"
                           ▼                             ▼
                        InMatch                       MainMenu
```

### `Services/GameSession.cs`

Singleton service registered in `Program.cs`. Responsibilities:

- Holds current `Phase` (MainMenu | ModeSetup | InMatch | MatchOver), current `Board`, active `Player`, mode flags, score counters.
- Exposes `OnChange` event so components re-render via `StateHasChanged`.
- `MakeMove(int row, int col)`: places the human's move; checks `Board.Winner()` / `Board.IsDraw()` and transitions to `MatchOver` if so; otherwise if vs CPU, awaits `Task.Delay(600)` (the "deliberation pause" gives the placement bloom animation time to play), calls `Ai.ChooseMove(...)`, applies it, re-checks for game end.
- `LoadScores()` / `SaveScores()`: JS interop (`IJSRuntime`) calls `localStorage.getItem` / `setItem` against the key `tictactoe.scores.v1`. Format: `{"ascendant": N, "vanguard": N, "draws": N}`.
- `ResetScores()`: zeros counters, persists.

### Components

- **`Game.razor`** — root. Reads `GameSession.Phase`, switches child component. ~80 lines.
- **`MainMenu.razor`** — two buttons (Hot-seat, Vs Computer). ~40 lines.
- **`ModeSetup.razor`** — two button groups (faction, difficulty), Start button. ~60 lines.
- **`BoardView.razor`** — renders a CSS Grid of nine `Cell` components. Receives `Board`, active player, and a `Func<int,int,Task>` click handler. Computes the winning-line cell coordinates if game is over and passes a `Highlight` flag to those cells. ~50 lines.
- **`Cell.razor`** — single cell. Accepts `Player` content, `IsHighlighted`, `IsHoverEnabled`, click handler. Renders empty / sigil / ghost-sigil-on-hover. ~40 lines.
- **`ScoreBoard.razor`** — three numbers + Reset button. ~30 lines.

## Visual Implementation (Cosmic Sigils)

### Palette & Typography

Defined as CSS custom properties in `theme.css` so changing the look is one place to edit.

```css
:root {
  /* Background layers */
  --bg-deep:      #0a0518;
  --bg-mid:       #1a1530;
  --bg-nebula:    #2a1a4a;

  /* Faction colors */
  --color-gold:           #ffd76b;
  --color-gold-glow:      rgba(255, 200, 80, 0.85);
  --color-crimson:        #ff7a9e;
  --color-crimson-glow:   rgba(255, 80, 140, 0.85);

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
  --asset-backdrop:        none;  /* Phase 1 = pure CSS; Phase 2 = url('/img/backdrop.png') */
}
```

### Phase 1 placeholders

- **Sigils.** Inline SVG files: `ascendant-sigil.svg` is a 12-rayed sun in a gold gradient with a soft outer glow (filter: blur). `vanguard-sigil.svg` is a 4-pointed sparkle with curved rays in a crimson-pink gradient with a similar glow. Both are 256×256 viewbox, transparent background, scale to any cell size.
- **Backdrop.** Pure CSS in `body`: layered radial-gradients at low opacity create a starfield (a dozen tiny dots scattered at named percentages), plus two large blurred radial gradients in `--bg-mid` and `--bg-nebula` create a nebula. No image file required; renders identically on any device.
- **Frame/UI chrome.** None. The cosmic direction is clean — sigils float on the starfield, separated by thin (1px) borders in low-opacity gold/crimson.

### Phase 2 PNG swap

Drop files into `web/wwwroot/img/` matching the spec in `wwwroot/img/README.md`, then change the `--asset-*` URLs in `theme.css`:

```css
--asset-ascendant-sigil: url('/img/ascendant-sigil.png');
--asset-vanguard-sigil:  url('/img/vanguard-sigil.png');
--asset-backdrop:        url('/img/backdrop.png');
```

If `--asset-backdrop` is not `none`, `body::before` adds it as a fixed background-image layered behind the CSS starfield.

### Asset specification (`wwwroot/img/README.md`)

| Slot | Phase 1 default | Phase 2 PNG | Min size | Notes |
|---|---|---|---|---|
| Ascendant sigil | inline SVG sun | gold sigil illustration | 256×256, transparent | Round bounding circle preferred |
| Vanguard sigil | inline SVG sparkle | crimson sigil illustration | 256×256, transparent | Round bounding circle preferred |
| Backdrop | pure CSS starfield | starfield/nebula image | 1920×1080, JPG ok | Tile- or fill-mode covered |
| Icon 192 | rendered placeholder | app icon | 192×192 PNG | Solid background OK |
| Icon 512 | rendered placeholder | app icon | 512×512 PNG | Used for splash on Android |
| Maskable 192/512 | placeholder | app icon w/ safe area | same | 80% safe zone, see Android docs |
| Apple touch icon | placeholder | iOS home-screen icon | 180×180 PNG | No transparency |
| Favicon | inline SVG | favicon.ico | 32×32 ICO | Browser tab |

### Animations (`animations.css`)

- **Placement bloom.** When a sigil appears: `transform: scale(0.5) → 1` and `opacity: 0 → 1` over 0.4s (cubic-bezier ease-out), with a `drop-shadow` that expands from the faction-color glow then settles. Triggered by adding a class via Blazor on placement.
- **Idle pulse.** Each placed sigil gently pulses: `opacity: 0.85 ↔ 1` and `filter: drop-shadow(... small) ↔ drop-shadow(... larger)` over 4s, infinite, ease-in-out. Subtle — must not distract.
- **Hover ghost.** When the active player hovers an empty cell, a 25%-opacity version of their sigil appears. Fades in over 0.15s.
- **Winning line.** When game ends with a winner: the three winning cells receive a class that adds an inset glowing border and a thin, faint connecting line (CSS pseudo-element with a gradient stroke from cell-center to cell-center) that animates in over 0.6s. The result banner appears 0.6s after, so the eye sees the line first.

### Mobile / responsive

- Grid: `aspect-ratio: 1 / 1`, `width: min(60vmin, 480px)`, centered.
- Buttons: `min-height: 48px` for tap targets.
- Below 480px viewport: menu/setup buttons stack vertically; score panel collapses to one line.
- All font sizes use `clamp(...)` for fluid scaling.
- Touch tested via Chrome DevTools at 375×667 (iPhone SE) and 390×844 (iPhone 14).

### PWA configuration

`wwwroot/manifest.webmanifest`:
```json
{
  "name": "Order vs Rebellion",
  "short_name": "Sector Duel",
  "description": "A duel of cosmic sigils.",
  "display": "standalone",
  "start_url": "/",
  "scope": "/",
  "theme_color": "#0a0518",
  "background_color": "#0a0518",
  "icons": [
    { "src": "/img/icons/icon-192.png", "sizes": "192x192", "type": "image/png" },
    { "src": "/img/icons/icon-512.png", "sizes": "512x512", "type": "image/png" },
    { "src": "/img/icons/icon-maskable-192.png", "sizes": "192x192", "type": "image/png", "purpose": "maskable" },
    { "src": "/img/icons/icon-maskable-512.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable" }
  ]
}
```

`index.html` head:
- `<link rel="manifest" href="manifest.webmanifest">`
- `<meta name="theme-color" content="#0a0518">`
- `<link rel="apple-touch-icon" href="/img/icons/apple-touch-icon.png">`
- `<meta name="apple-mobile-web-app-capable" content="yes">`
- `<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent">`

Service worker is the Blazor WASM PWA template default (caches all build assets, refreshes on new version).

## Persistence

`localStorage["tictactoe.scores.v1"]` stores `{ascendant, vanguard, draws}` as JSON. `GameSession` reads it on startup (or starts at 0/0/0 if missing or malformed) and writes it on every score change. A "Reset score" button on the score panel zeros it. The `v1` suffix in the key reserves room for future schema changes.

## Error Handling

- Invalid JSON in localStorage → log to console, fall back to 0/0/0.
- AI move computation should be instantaneous; if it ever throws, log to console and end the match as a draw with a friendly "subspace fault" banner.
- Service worker registration failure (e.g., `file://` access) → site still works, just no offline. No UI surface required.
- No user-facing input parsing — clicks are constrained to valid empty cells via component state.

## Testing Strategy (manual checklist)

The original console smoke tests still run from `console/tests/smoke.ps1` after the refactor (mandatory: 10/10). For the web app v1, a manual checklist:

1. **Build:** `dotnet build` from repo root succeeds, 0 warnings.
2. **Run:** `dotnet run --project web` starts the dev server; browser opens.
3. **Hot-seat row win:** click A1, A2, B1, B2, C1 (alternating). Top row highlights gold. Banner reads "The Ascendant holds the line — three sectors in a row." Score: 1 / 0 / 0.
4. **Hot-seat column win:** column A win for either side; column highlights.
5. **Hot-seat diagonal win:** A1/B2/C3 wins; diagonal line traces.
6. **Hot-seat draw:** play to a stalemate; "Stalemate" banner; score: 0 / 0 / 1.
7. **Vs Recruit:** play 3 games; AI picks plausibly random cells; human can win.
8. **Vs Captain:** AI blocks an obvious one-move threat.
9. **Vs Warlord:** play 3 games; human never wins (draws or losses only).
10. **Reload page:** scores from previous match are still there.
11. **Reset score button:** zeros all three.
12. **Mobile size (375×667):** grid stays square and within viewport; buttons tap-sized.
13. **Install PWA (desktop Chrome):** install button appears in URL bar; installed app launches in standalone window.
14. **Offline play:** disconnect network; reload; game still works.
15. **Lighthouse audit:** PWA score ≥ 90.

## Decisions Locked (during brainstorming)

| # | Decision | Source |
|---|---|---|
| 1 | Tech stack: Blazor WebAssembly | user picked option A |
| 2 | Code sharing: shared `core/` class library | user approved option A |
| 3 | Folder strictly separated from `console/` | user explicit instruction |
| 4 | Visual direction: Cosmic Sigils | user picked option C |
| 5 | Two-phase visuals (CSS placeholders → PNG swap) | user explicit guidance |
| 6 | v1 includes ambitious animations + localStorage + winning-line highlight | user "recommended for now" |
| 7 | PWA installable on desktop and mobile | user yes |
| 8 | No sound effects in v1 | recommendation, accepted |
| 9 | Mobile-responsive baseline at 375px width | implied by PWA-on-mobile |

## Out of Scope (deferred)

- Multiplayer, networking, accounts.
- Sound and music.
- Alternative themes / theme switcher.
- Accessibility audit beyond keyboard-clickable cells and reasonable contrast.
- Tournaments, AI-vs-AI replays.
- Backend / leaderboards / save sync.
- Server deployment / CI / hosted URL.
- Internationalization.

## File Inventory at Completion

```
tic-tac-toe-game/
├── TicTacToe.sln
├── docs/superpowers/specs/2026-05-06-tic-tac-toe-web-design.md  (this file)
├── docs/superpowers/plans/2026-05-06-tic-tac-toe-web-plan.md    (next step)
├── core/                  (NEW class library, ~5 files)
├── console/               (refactored — Board.cs/Ai.cs replaced by core ref)
└── web/                   (NEW Blazor WASM PWA, ~30 files incl. wwwroot)
```
