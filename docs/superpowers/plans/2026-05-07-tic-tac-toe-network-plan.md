# Tic-Tac-Toe Network Multiplayer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a third sibling app at `web-multiplayer/` — a Blazor Server tic-tac-toe with three play modes (hot-seat, vs CPU, network multiplayer with auto-named lobby + challenge UX), reusing the existing `core/` library unchanged.

**Architecture:** Single ASP.NET Core 9 / Blazor Server project. UI runs server-side over a SignalR connection per client. Local modes (hot-seat, vs CPU) use a per-circuit `LocalGameSession`. Network mode adds a `GameHub` SignalR endpoint plus three singleton services (`PresenceTracker`, `MatchRegistry`, `ChallengeBroker`) and a `NameGenerator` static helper. All multiplayer state is in-memory; no DB. Deploy artifacts are operator-agnostic — env-var-driven `deploy.sh` and a systemd unit template with placeholders.

**Tech Stack:** .NET 9 SDK, ASP.NET Core, Blazor Server, SignalR (built-in), Razor components. No NuGet additions beyond the template defaults. `core/TicTacToe.Core` referenced as a project reference.

**Note on testing:** Matching the rest of the repo, no automated test project. The console smoke checklist at `console/tests/smoke.ps1` keeps passing 10/10 (regression bar — `core/` is untouched). Web-multiplayer v1 acceptance is a 13-step manual smoke checklist (Task 21).

---

## File Structure

| File | Status | Responsibility |
|---|---|---|
| `web-multiplayer/TicTacToe.WebMultiplayer.csproj` | NEW | Project file; references core |
| `web-multiplayer/Program.cs` | NEW | DI setup, hub mapping, routing |
| `web-multiplayer/App.razor` | NEW (template) | Router |
| `web-multiplayer/_Imports.razor` | NEW | `@using` directives |
| `web-multiplayer/MainLayout.razor` | NEW | Minimal layout shell |
| `web-multiplayer/Pages/_Host.cshtml` | NEW (template) | Server page |
| `web-multiplayer/Pages/Index.razor` | NEW | Routes `/` to root component |
| `web-multiplayer/Components/Game.razor` | NEW | Root state-machine component |
| `web-multiplayer/Components/MainMenu.razor` | NEW | 3-button menu |
| `web-multiplayer/Components/ModeSetup.razor` | NEW | Faction + difficulty for vs CPU |
| `web-multiplayer/Components/BoardView.razor` | NEW (copied from web/) | 3×3 grid |
| `web-multiplayer/Components/Cell.razor` | NEW (copied from web/) | Single cell |
| `web-multiplayer/Components/ScoreBoard.razor` | NEW (copied from web/) | Score panel |
| `web-multiplayer/Components/Lobby.razor` | NEW | Online players + challenge buttons |
| `web-multiplayer/Components/ChallengePrompt.razor` | NEW | Modal for incoming challenge |
| `web-multiplayer/Components/DisconnectBanner.razor` | NEW | Countdown for opponent grace period |
| `web-multiplayer/Hubs/GameHub.cs` | NEW | SignalR endpoint |
| `web-multiplayer/Services/LocalGameSession.cs` | NEW | Per-circuit local-mode state |
| `web-multiplayer/Services/PresenceTracker.cs` | NEW | Singleton: who's online |
| `web-multiplayer/Services/NameGenerator.cs` | NEW | Static: name from connection ID |
| `web-multiplayer/Services/MatchRegistry.cs` | NEW | Singleton: active multiplayer matches |
| `web-multiplayer/Services/ChallengeBroker.cs` | NEW | Singleton: pending challenges with TTL |
| `web-multiplayer/wwwroot/css/theme.css` | NEW (copied) | Cosmic palette + typography |
| `web-multiplayer/wwwroot/css/board.css` | NEW (copied) | Grid + cells |
| `web-multiplayer/wwwroot/css/animations.css` | NEW (copied) | Placement bloom, idle pulse, win glow |
| `web-multiplayer/wwwroot/css/multiplayer.css` | NEW | Lobby, challenge prompt, disconnect banner |
| `web-multiplayer/wwwroot/img/ascendant-sigil.svg` | NEW (copied) | Gold sun |
| `web-multiplayer/wwwroot/img/vanguard-sigil.svg` | NEW (copied) | Crimson sparkle |
| `web-multiplayer/deploy/deploy.sh` | NEW | Env-var-driven deploy |
| `web-multiplayer/deploy/tictactoe-multi.service.template` | NEW | Systemd unit template |
| `web-multiplayer/README.md` | NEW | Run + deploy instructions |
| `README.md` | MODIFY | Add `web-multiplayer/` row to apps table + run command |
| `TicTacToe.sln` | MODIFY | Register the new project |

---

## Task 1: Scaffold the project

**Files:**
- Create: `D:/AI/tic-tac-toe-game/web-multiplayer/` (whole directory tree from `dotnet new`)
- Modify: `D:/AI/tic-tac-toe-game/TicTacToe.sln`

- [ ] **Step 1: Scaffold via `dotnet new blazor`**

Run from `./` (repo root):
```powershell
dotnet new blazor --interactivity Server --use-program-main --name TicTacToe.WebMultiplayer --output web-multiplayer --framework net9.0 --auth None --empty
```
Expected: creates `web-multiplayer/TicTacToe.WebMultiplayer.csproj`, `Program.cs`, `App.razor`, `_Imports.razor`, `Components/` (template stubs), `wwwroot/`, etc.

If the template's flag set differs in your .NET version, the working alternative is:
```powershell
dotnet new blazorserver --name TicTacToe.WebMultiplayer --output web-multiplayer --framework net9.0
```
(Older template; stripping boilerplate works the same way in Step 4 either way.)

- [ ] **Step 2: Add the project to the solution**

```powershell
dotnet sln TicTacToe.sln add web-multiplayer/TicTacToe.WebMultiplayer.csproj
```

- [ ] **Step 3: Add a project reference from web-multiplayer to core**

Edit `web-multiplayer/TicTacToe.WebMultiplayer.csproj`. Add this `<ItemGroup>` (alongside any existing ones):

```xml
<ItemGroup>
  <ProjectReference Include="..\core\TicTacToe.Core.csproj" />
</ItemGroup>
```

- [ ] **Step 4: Strip template boilerplate**

Remove the placeholder pages and weather-style demos that ship with the template. The exact filenames vary by .NET 9 template version. Run:

```powershell
Remove-Item web-multiplayer/Components/Pages/Counter.razor -ErrorAction SilentlyContinue
Remove-Item web-multiplayer/Components/Pages/Weather.razor -ErrorAction SilentlyContinue
Remove-Item web-multiplayer/Components/Pages/Home.razor -ErrorAction SilentlyContinue
Remove-Item web-multiplayer/Components/Layout/NavMenu.razor -ErrorAction SilentlyContinue
Remove-Item web-multiplayer/Components/Layout/NavMenu.razor.css -ErrorAction SilentlyContinue
Remove-Item web-multiplayer/Components/Layout/MainLayout.razor.css -ErrorAction SilentlyContinue
Remove-Item web-multiplayer/wwwroot/lib -Recurse -ErrorAction SilentlyContinue
Remove-Item web-multiplayer/wwwroot/bootstrap -Recurse -ErrorAction SilentlyContinue
```

- [ ] **Step 5: Replace `Components/Layout/MainLayout.razor` with a minimal shell**

Overwrite (or create) `web-multiplayer/Components/Layout/MainLayout.razor`:

```razor
@inherits LayoutComponentBase

<main class="app-main-layout">
    @Body
</main>
```

If the file lives at `web-multiplayer/MainLayout.razor` instead (older template), put the same content there.

- [ ] **Step 6: Add a routable home page that hosts the root component**

Create `web-multiplayer/Components/Pages/Index.razor`:

```razor
@page "/"
@using TicTacToe.WebMultiplayer.Components

<Game />
```

(Or `web-multiplayer/Pages/Index.razor` if the template uses the older non-Components layout — the file just needs `@page "/"` and to render `<Game />`. `Game.razor` is created in a later task; for now, comment out the `<Game />` line to allow the build to pass — uncomment in Task 14.)

For now, replace `<Game />` with placeholder content so the project builds:
```razor
@page "/"

<h1>Tic-Tac-Toe Web-Multiplayer — coming up</h1>
```

- [ ] **Step 7: Verify build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: `Build succeeded. 0 Warning(s). 0 Error(s).` for all four projects: TicTacToe.Core, TicTacToe.Console, TicTacToe.Web, TicTacToe.WebMultiplayer.

- [ ] **Step 8: Sanity-run**

```powershell
dotnet run --project web-multiplayer --urls http://localhost:5050
```
Open http://localhost:5050 — should see the placeholder heading. Stop with Ctrl+C.

---

## Task 2: Copy visual assets and CSS from `web/`

The cosmic theme is identical between the two web apps. Re-using the same files keeps them visually in sync.

**Files:**
- Create (copied): `web-multiplayer/wwwroot/css/theme.css`
- Create (copied): `web-multiplayer/wwwroot/css/board.css`
- Create (copied): `web-multiplayer/wwwroot/css/animations.css`
- Create (copied): `web-multiplayer/wwwroot/img/ascendant-sigil.svg`
- Create (copied): `web-multiplayer/wwwroot/img/vanguard-sigil.svg`
- Create: `web-multiplayer/wwwroot/img/README.md` (asset spec — copied from web/)
- Modify: `web-multiplayer/Components/_Imports.razor` (or root _Imports) — add usings
- Modify: the page that hosts the app (`App.razor` or `_Host.cshtml`) — link the CSS files

- [ ] **Step 1: Copy CSS files**

```powershell
mkdir web-multiplayer/wwwroot/css -Force
Copy-Item web/wwwroot/css/theme.css       web-multiplayer/wwwroot/css/theme.css
Copy-Item web/wwwroot/css/board.css       web-multiplayer/wwwroot/css/board.css
Copy-Item web/wwwroot/css/animations.css  web-multiplayer/wwwroot/css/animations.css
```

- [ ] **Step 2: Copy SVG sigils + asset README**

```powershell
mkdir web-multiplayer/wwwroot/img -Force
Copy-Item web/wwwroot/img/ascendant-sigil.svg web-multiplayer/wwwroot/img/ascendant-sigil.svg
Copy-Item web/wwwroot/img/vanguard-sigil.svg  web-multiplayer/wwwroot/img/vanguard-sigil.svg
Copy-Item web/wwwroot/img/README.md           web-multiplayer/wwwroot/img/README.md
```

- [ ] **Step 3: Wire the CSS into the host page**

Find the file where the template emits the `<head>` — in .NET 9 Blazor templates this is usually `web-multiplayer/Components/App.razor`. Open it; the head currently has the template's CSS link. Add the three custom CSS files **after** any template default CSS so they win the cascade:

```razor
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href="/" />
    <title>Order vs Rebellion — Multiplayer</title>

    <meta name="theme-color" content="#0a0518" />
    <meta name="description" content="A duel of cosmic sigils — multiplayer." />

    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Spectral:ital,wght@0,400;0,600;1,400&family=Inter:wght@400;600&display=swap" rel="stylesheet" />

    <link rel="stylesheet" href="@Assets["css/theme.css"]" />
    <link rel="stylesheet" href="@Assets["css/board.css"]" />
    <link rel="stylesheet" href="@Assets["css/animations.css"]" />
    <link rel="stylesheet" href="@Assets["css/multiplayer.css"]" />
    <HeadOutlet @rendermode="InteractiveServer" />
</head>
```

If the template uses the older `_Host.cshtml` shape, edit `web-multiplayer/Pages/_Host.cshtml`'s `<head>` similarly (using plain `~/css/...` paths instead of `@Assets[...]`).

`css/multiplayer.css` doesn't exist yet — Task 11 creates it. Reference it now to avoid revisiting this file.

- [ ] **Step 4: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: clean build. (CSS files don't affect the .NET build; verify nothing else broke.)

---

## Task 3: Implement `NameGenerator`

A static, pure function. Good first service to land — easy to verify in isolation by spot-checking the output.

**Files:**
- Create: `web-multiplayer/Services/NameGenerator.cs`

- [ ] **Step 1: Write the file**

```csharp
using System;
using System.Security.Cryptography;
using System.Text;

namespace TicTacToe.WebMultiplayer.Services;

public static class NameGenerator
{
    private static readonly string[] Adjectives = new[]
    {
        "Quiet", "Bold", "Crimson", "Azure", "Golden", "Silver", "Distant",
        "Brave", "Swift", "Cosmic", "Nebula", "Stellar", "Lunar", "Solar",
        "Astral", "Ember", "Frost", "Twilight", "Dawn", "Ancient",
        "Bright", "Hidden", "Steady", "Restless", "Patient", "Wandering",
        "Daring", "Gentle", "Fierce", "Clever", "Watchful", "Eager",
        "Noble", "Quick", "Steep", "Vast", "Polar", "Tidal", "Velvet", "Iron",
    };

    private static readonly string[] Nouns = new[]
    {
        "Comet", "Wanderer", "Sigil", "Phoenix", "Voyager", "Nova", "Pulsar",
        "Sentinel", "Captain", "Navigator", "Pilot", "Outrider", "Drifter",
        "Falcon", "Beacon", "Herald", "Echo", "Spark", "Tide", "Glimmer",
        "Cinder", "Wisp", "Cipher", "Talon", "Veil", "Lantern", "Anchor",
        "Mirror", "Gale", "Vow", "Reaver", "Watcher", "Strider", "Sailor",
        "Rider", "Guardian", "Scout", "Ranger", "Skipper", "Dreamer",
    };

    public static string For(string connectionId)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(connectionId), hash);
        var adj  = Adjectives[BitConverter.ToUInt16(hash[..2]) % Adjectives.Length];
        var noun = Nouns[BitConverter.ToUInt16(hash.Slice(2, 2)) % Nouns.Length];
        return $"{adj} {noun}";
    }
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: clean build.

- [ ] **Step 3: Manual sanity check via dotnet-script (optional)**

If you have `dotnet-script` installed, paste the function call to confirm names look reasonable:

```powershell
echo 'using TicTacToe.WebMultiplayer.Services; for (int i = 0; i < 5; i++) Console.WriteLine(NameGenerator.For(Guid.NewGuid().ToString()));' | dotnet script -
```

If not, this gets exercised in real use during Task 17 (lobby flow). Skip the check.

---

## Task 4: Implement `PresenceTracker`

Singleton service tracking which connection IDs are currently in the lobby and what name each has.

**Files:**
- Create: `web-multiplayer/Services/PresenceTracker.cs`

- [ ] **Step 1: Write the file**

```csharp
using System;
using System.Collections.Generic;

namespace TicTacToe.WebMultiplayer.Services;

public sealed record PlayerSession(
    string ConnectionId,
    string Name,
    DateTimeOffset ConnectedAt
);

public sealed class PresenceTracker
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PlayerSession> _sessions = new();
    private readonly Dictionary<string, string> _matchByConnection = new();

    public event Action? Changed;

    public PlayerSession Add(string connectionId)
    {
        PlayerSession session;
        lock (_gate)
        {
            // Generate a unique name (suffix duplicates).
            var baseName = NameGenerator.For(connectionId);
            var name = baseName;
            int suffix = 2;
            while (NameTaken(name)) name = $"{baseName} {suffix++}";

            session = new PlayerSession(connectionId, name, DateTimeOffset.UtcNow);
            _sessions[connectionId] = session;
        }
        Changed?.Invoke();
        return session;
    }

    public PlayerSession? Remove(string connectionId)
    {
        PlayerSession? removed = null;
        lock (_gate)
        {
            if (_sessions.Remove(connectionId, out var s)) removed = s;
            _matchByConnection.Remove(connectionId);
        }
        if (removed != null) Changed?.Invoke();
        return removed;
    }

    public PlayerSession? Get(string connectionId)
    {
        lock (_gate) return _sessions.TryGetValue(connectionId, out var s) ? s : null;
    }

    public IReadOnlyList<PlayerSession> SnapshotLobby()
    {
        // "Lobby" = sessions not currently in a match.
        lock (_gate)
        {
            var list = new List<PlayerSession>(_sessions.Count);
            foreach (var kv in _sessions)
                if (!_matchByConnection.ContainsKey(kv.Key))
                    list.Add(kv.Value);
            return list;
        }
    }

    public void EnterMatch(string connectionId, string matchId)
    {
        lock (_gate) _matchByConnection[connectionId] = matchId;
        Changed?.Invoke();
    }

    public void LeaveMatch(string connectionId)
    {
        lock (_gate) _matchByConnection.Remove(connectionId);
        Changed?.Invoke();
    }

    public string? CurrentMatchOf(string connectionId)
    {
        lock (_gate) return _matchByConnection.TryGetValue(connectionId, out var id) ? id : null;
    }

    private bool NameTaken(string name)
    {
        // _gate already held by caller.
        foreach (var s in _sessions.Values)
            if (s.Name == name) return true;
        return false;
    }
}
```

- [ ] **Step 2: Register in DI**

Open `web-multiplayer/Program.cs`. Add the service registration alongside the existing `AddRazorComponents()` call:

```csharp
using TicTacToe.WebMultiplayer.Services;

// ... after AddRazorComponents() etc.
builder.Services.AddSingleton<PresenceTracker>();
```

- [ ] **Step 3: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: clean build.

---

## Task 5: Implement `MatchRegistry`

Stores active multiplayer matches. Each match holds a `Board` from `core/`, the two participants' connection IDs and factions, and a timer for disconnect grace.

**Files:**
- Create: `web-multiplayer/Services/MatchRegistry.cs`

- [ ] **Step 1: Write the file**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using TicTacToe.Core;

namespace TicTacToe.WebMultiplayer.Services;

public sealed class MultiplayerMatch
{
    public required string MatchId { get; init; }
    public required string AscendantConnId { get; set; }
    public required string VanguardConnId { get; set; }
    public required string AscendantName { get; init; }
    public required string VanguardName { get; init; }
    public Board Board { get; } = new();
    public Player Active { get; set; } = Player.Ascendant;
    public Player? Winner { get; set; }
    public bool IsDraw { get; set; }
    public (int row, int col)[]? WinningCells { get; set; }
    public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;
    public string? DisconnectedConnId { get; set; }
    public Timer? DisconnectTimer { get; set; }
}

public sealed record PlaceResult(bool Accepted, MultiplayerMatch? Match, string? Reason);

public sealed class MatchRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, MultiplayerMatch> _matches = new();

    public MultiplayerMatch Create(string ascConnId, string ascName, string vanConnId, string vanName)
    {
        var match = new MultiplayerMatch
        {
            MatchId = Guid.NewGuid().ToString("N")[..10],
            AscendantConnId = ascConnId,
            VanguardConnId = vanConnId,
            AscendantName = ascName,
            VanguardName = vanName,
        };
        lock (_gate) _matches[match.MatchId] = match;
        return match;
    }

    public MultiplayerMatch? Get(string matchId)
    {
        lock (_gate) return _matches.TryGetValue(matchId, out var m) ? m : null;
    }

    public PlaceResult TryPlace(string matchId, string fromConnId, int row, int col)
    {
        lock (_gate)
        {
            if (!_matches.TryGetValue(matchId, out var m))
                return new PlaceResult(false, null, "no such match");
            if (m.Winner != null || m.IsDraw)
                return new PlaceResult(false, m, "match already ended");
            var fromFaction = m.AscendantConnId == fromConnId ? Player.Ascendant
                            : m.VanguardConnId  == fromConnId ? Player.Vanguard
                            : Player.None;
            if (fromFaction == Player.None)
                return new PlaceResult(false, m, "not a participant");
            if (m.Active != fromFaction)
                return new PlaceResult(false, m, "not your turn");
            if (!m.Board.TryPlace(row, col, fromFaction))
                return new PlaceResult(false, m, "invalid cell");

            m.LastActivityAt = DateTimeOffset.UtcNow;

            var winner = m.Board.Winner();
            if (winner is { } w)
            {
                m.Winner = w;
                m.WinningCells = ComputeWinningCells(m.Board);
            }
            else if (m.Board.IsDraw())
            {
                m.IsDraw = true;
            }
            else
            {
                m.Active = Board.Other(m.Active);
            }
            return new PlaceResult(true, m, null);
        }
    }

    public MultiplayerMatch? Forfeit(string matchId, string forfeiterConnId)
    {
        lock (_gate)
        {
            if (!_matches.TryGetValue(matchId, out var m)) return null;
            if (m.Winner != null || m.IsDraw) return m;
            // The OTHER player wins.
            m.Winner = m.AscendantConnId == forfeiterConnId ? Player.Vanguard : Player.Ascendant;
            return m;
        }
    }

    public bool Remove(string matchId)
    {
        lock (_gate) return _matches.Remove(matchId);
    }

    public IEnumerable<string> ActiveMatchesContaining(string connectionId)
    {
        lock (_gate)
        {
            var ids = new List<string>();
            foreach (var m in _matches.Values)
                if (m.AscendantConnId == connectionId || m.VanguardConnId == connectionId)
                    ids.Add(m.MatchId);
            return ids;
        }
    }

    private static (int row, int col)[]? ComputeWinningCells(Board b)
    {
        for (int r = 0; r < 3; r++)
            if (b.At(r, 0) != Player.None && b.At(r, 0) == b.At(r, 1) && b.At(r, 1) == b.At(r, 2))
                return new[] { (r, 0), (r, 1), (r, 2) };
        for (int c = 0; c < 3; c++)
            if (b.At(0, c) != Player.None && b.At(0, c) == b.At(1, c) && b.At(1, c) == b.At(2, c))
                return new[] { (0, c), (1, c), (2, c) };
        if (b.At(0, 0) != Player.None && b.At(0, 0) == b.At(1, 1) && b.At(1, 1) == b.At(2, 2))
            return new[] { (0, 0), (1, 1), (2, 2) };
        if (b.At(0, 2) != Player.None && b.At(0, 2) == b.At(1, 1) && b.At(1, 1) == b.At(2, 0))
            return new[] { (0, 2), (1, 1), (2, 0) };
        return null;
    }
}
```

- [ ] **Step 2: Register in DI**

In `web-multiplayer/Program.cs`:
```csharp
builder.Services.AddSingleton<MatchRegistry>();
```

- [ ] **Step 3: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: clean build.

---

## Task 6: Implement `ChallengeBroker`

Tracks pending challenges. Each has a 30-second TTL; an internal timer scans every 5s and expires stale ones.

**Files:**
- Create: `web-multiplayer/Services/ChallengeBroker.cs`

- [ ] **Step 1: Write the file**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;

namespace TicTacToe.WebMultiplayer.Services;

public sealed record Challenge(
    string ChallengeId,
    string FromConnId,
    string FromName,
    string ToConnId,
    string ToName,
    DateTimeOffset ExpiresAt
);

public sealed class ChallengeBroker : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Challenge> _challenges = new();
    private readonly Timer _sweeper;
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromSeconds(30);

    public event Action<Challenge>? Expired;

    public ChallengeBroker()
    {
        _sweeper = new Timer(_ => SweepExpired(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public Challenge Create(string fromConn, string fromName, string toConn, string toName)
    {
        var c = new Challenge(
            ChallengeId: Guid.NewGuid().ToString("N")[..10],
            FromConnId: fromConn,
            FromName: fromName,
            ToConnId: toConn,
            ToName: toName,
            ExpiresAt: DateTimeOffset.UtcNow + Lifetime);
        lock (_gate) _challenges[c.ChallengeId] = c;
        return c;
    }

    public Challenge? Take(string challengeId)
    {
        lock (_gate)
        {
            if (_challenges.Remove(challengeId, out var c)) return c;
            return null;
        }
    }

    public bool Cancel(string challengeId, string fromConnId)
    {
        lock (_gate)
        {
            if (_challenges.TryGetValue(challengeId, out var c) && c.FromConnId == fromConnId)
                return _challenges.Remove(challengeId);
            return false;
        }
    }

    public void RemoveAllInvolving(string connectionId)
    {
        List<Challenge> toRemove = new();
        lock (_gate)
        {
            foreach (var c in _challenges.Values)
                if (c.FromConnId == connectionId || c.ToConnId == connectionId)
                    toRemove.Add(c);
            foreach (var c in toRemove) _challenges.Remove(c.ChallengeId);
        }
    }

    private void SweepExpired()
    {
        var now = DateTimeOffset.UtcNow;
        List<Challenge> expired = new();
        lock (_gate)
        {
            foreach (var (id, c) in _challenges)
                if (c.ExpiresAt <= now) expired.Add(c);
            foreach (var c in expired) _challenges.Remove(c.ChallengeId);
        }
        foreach (var c in expired) Expired?.Invoke(c);
    }

    public void Dispose() => _sweeper.Dispose();
}
```

- [ ] **Step 2: Register in DI**

In `web-multiplayer/Program.cs`:
```csharp
builder.Services.AddSingleton<ChallengeBroker>();
```

- [ ] **Step 3: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: clean build.

---

## Task 7: Implement `LocalGameSession` (hot-seat + vs CPU)

Adapts the v2 web app's `GameSession`. Differences: no JS interop (no localStorage — Blazor Server can persist via server-side state, but for v1 keep score in-memory per circuit). Two-mode (hot-seat + vs CPU) only — no network mode.

**Files:**
- Create: `web-multiplayer/Services/LocalGameSession.cs`

- [ ] **Step 1: Write the file**

```csharp
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
```

- [ ] **Step 2: Register in DI as scoped**

In `web-multiplayer/Program.cs`:
```csharp
builder.Services.AddScoped<LocalGameSession>();
```

`Scoped` for Blazor Server = one instance per circuit (per browser tab).

- [ ] **Step 3: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: clean build.

---

## Task 8: Copy `Cell.razor` and `BoardView.razor` from web/

Identical components — they only know about `Board` from `core/` and a click event.

**Files:**
- Create (copied): `web-multiplayer/Components/Cell.razor`
- Create (copied): `web-multiplayer/Components/BoardView.razor`

- [ ] **Step 1: Copy the files**

```powershell
mkdir web-multiplayer/Components -Force
Copy-Item web/Components/Cell.razor      web-multiplayer/Components/Cell.razor
Copy-Item web/Components/BoardView.razor web-multiplayer/Components/BoardView.razor
```

- [ ] **Step 2: Add `_Imports.razor` so the components resolve**

Edit (or create) `web-multiplayer/Components/_Imports.razor` to add:
```razor
@using TicTacToe.Core
@using TicTacToe.WebMultiplayer.Services
@using TicTacToe.WebMultiplayer.Components
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
```

If the .NET 9 template already has `_Imports.razor` at `web-multiplayer/_Imports.razor`, append the three project-specific usings (`Core`, `Services`, `Components`) there instead.

- [ ] **Step 3: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: clean build.

---

## Task 9: Copy and adapt `MainMenu.razor`, `ModeSetup.razor`, `ScoreBoard.razor`

`ModeSetup` and `ScoreBoard` are unchanged. `MainMenu` gets a third button.

**Files:**
- Create: `web-multiplayer/Components/MainMenu.razor`
- Create (copied): `web-multiplayer/Components/ModeSetup.razor`
- Create (copied): `web-multiplayer/Components/ScoreBoard.razor`

- [ ] **Step 1: Copy ModeSetup and ScoreBoard**

```powershell
Copy-Item web/Components/ModeSetup.razor   web-multiplayer/Components/ModeSetup.razor
Copy-Item web/Components/ScoreBoard.razor  web-multiplayer/Components/ScoreBoard.razor
```

- [ ] **Step 2: Write the new `MainMenu.razor`**

```razor
<div class="menu-panel">
    <h1 class="title">Order vs Rebellion</h1>
    <p class="tagline">A duel of cosmic sigils.</p>
    <div class="menu-buttons">
        <button class="btn btn-primary" @onclick="OnHotSeat">Hot-seat (two humans)</button>
        <button class="btn btn-primary" @onclick="OnVsComputer">Versus the computer</button>
        <button class="btn btn-primary" @onclick="OnNetwork">Network multiplayer</button>
    </div>
</div>

@code {
    [Parameter] public EventCallback OnHotSeat { get; set; }
    [Parameter] public EventCallback OnVsComputer { get; set; }
    [Parameter] public EventCallback OnNetwork { get; set; }
}
```

- [ ] **Step 3: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: clean build.

---

## Task 10: Build local-mode `Game.razor` (hot-seat + vs CPU only — networking added in Task 17)

This task wires up the local modes only. Network buttons go to a placeholder. Confirms the local UI works end-to-end before adding multiplayer complexity.

**Files:**
- Create: `web-multiplayer/Components/Game.razor`
- Modify: `web-multiplayer/Components/Pages/Index.razor` (replace placeholder with `<Game />`)

- [ ] **Step 1: Write `Game.razor`**

```razor
@implements IDisposable
@inject LocalGameSession Local

<div class="app-shell">
    <main class="app-main">
        @switch (_phase)
        {
            case AppPhase.MainMenu:
                <MainMenu OnHotSeat="StartHotSeat"
                          OnVsComputer="GoToModeSetup"
                          OnNetwork="GoToNetwork" />
                break;

            case AppPhase.LocalModeSetup:
                <ModeSetup OnBack="GoToMainMenu"
                           OnStart="StartVersusComputer" />
                break;

            case AppPhase.LocalInMatch:
            case AppPhase.LocalMatchOver:
                <div class="match-panel">
                    <div class="status-banner">
                        @if (_phase == AppPhase.LocalMatchOver)
                        {
                            @if (Local.Winner is { } w)
                            {
                                <span class="status-text status-@FactionClass(w)">@ThemeText.WinMessage(w)</span>
                            }
                            else if (Local.IsDraw)
                            {
                                <span class="status-text">@ThemeText.DrawMessage()</span>
                            }
                        }
                        else if (Local.AiThinking)
                        {
                            <span class="status-text status-@FactionClass(Local.Active) status-thinking">
                                @ThemeText.NameOf(Local.Active) considers the grid…
                            </span>
                        }
                        else
                        {
                            <span class="status-text status-@FactionClass(Local.Active)">
                                @ThemeText.NameOf(Local.Active) — claim a sector
                            </span>
                        }
                    </div>

                    <BoardView Board="Local.Board"
                               HoverPlayer="HoverPlayer"
                               AcceptingClicks="@(_phase == AppPhase.LocalInMatch && !Local.AiThinking && (!Local.VersusComputer || Local.Active == Local.HumanSide))"
                               WinningCells="Local.WinningCells"
                               OnCellClick="OnCellClick" />

                    <ScoreBoard AscendantWins="Local.AscendantWins"
                                VanguardWins="Local.VanguardWins"
                                Draws="Local.Draws"
                                ShowReset="true"
                                OnReset="ResetScore" />

                    @if (_phase == AppPhase.LocalMatchOver)
                    {
                        <div class="menu-buttons">
                            <button class="btn btn-primary" @onclick="PlayAgain">Play again</button>
                            <button class="btn btn-secondary" @onclick="GoToMainMenu">Main menu</button>
                        </div>
                    }
                </div>
                break;

            case AppPhase.NetworkPlaceholder:
                <div class="menu-panel">
                    <h2 class="subtitle">Network multiplayer</h2>
                    <p class="tagline">Coming up in Task 17.</p>
                    <div class="menu-buttons">
                        <button class="btn btn-secondary" @onclick="GoToMainMenu">Back</button>
                    </div>
                </div>
                break;
        }
    </main>
</div>

@code {
    private enum AppPhase
    {
        MainMenu, LocalModeSetup, LocalInMatch, LocalMatchOver, NetworkPlaceholder
    }

    private AppPhase _phase = AppPhase.MainMenu;

    private Player HoverPlayer => Local.VersusComputer ? Local.HumanSide : Local.Active;

    protected override void OnInitialized()
    {
        Local.OnChange += HandleLocalChange;
    }

    private void HandleLocalChange()
    {
        _phase = Local.Phase switch
        {
            LocalPhase.MainMenu   => AppPhase.MainMenu,
            LocalPhase.ModeSetup  => AppPhase.LocalModeSetup,
            LocalPhase.InMatch    => AppPhase.LocalInMatch,
            LocalPhase.MatchOver  => AppPhase.LocalMatchOver,
            _                     => _phase
        };
        InvokeAsync(StateHasChanged);
    }

    public void Dispose() => Local.OnChange -= HandleLocalChange;

    private void StartHotSeat() => Local.StartHotSeat();
    private void GoToModeSetup() => Local.GoToModeSetup();

    private void GoToMainMenu()
    {
        _phase = AppPhase.MainMenu;
        Local.GoToMainMenu();
    }

    private void GoToNetwork() { _phase = AppPhase.NetworkPlaceholder; }

    private async Task StartVersusComputer((Player faction, Difficulty difficulty) args)
        => await Local.StartVersusComputerAsync(args.faction, args.difficulty);

    private async Task OnCellClick((int row, int col) move)
        => await Local.TryHumanMoveAsync(move.row, move.col);

    private async Task PlayAgain() => await Local.PlayAgainAsync();
    private void ResetScore() => Local.ResetScores();

    private static string FactionClass(Player p) => p switch
    {
        Player.Ascendant => "ascendant",
        Player.Vanguard  => "vanguard",
        _                => "none"
    };
}
```

- [ ] **Step 2: Update `Index.razor` to render `<Game />`**

Replace `web-multiplayer/Components/Pages/Index.razor` (or `Pages/Index.razor`) with:

```razor
@page "/"

<Game />
```

- [ ] **Step 3: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: clean build.

- [ ] **Step 4: Run + smoke-test local modes**

```powershell
dotnet run --project web-multiplayer --urls http://localhost:5050
```

Open http://localhost:5050 in a browser:
- Title and three buttons render with cosmic theme.
- Click "Hot-seat" → board appears → play a row-win → win banner appears → score updates.
- Click "Network multiplayer" → placeholder card appears → "Back" returns to main menu.
- Click "Versus the computer" → mode setup appears → pick Ascendant + Warlord → match begins → AI plays after ~600ms; verify Warlord doesn't lose.

Stop with Ctrl+C.

---

## Task 11: Author `multiplayer.css` (lobby + challenge + disconnect banner styles)

**Files:**
- Create: `web-multiplayer/wwwroot/css/multiplayer.css`

- [ ] **Step 1: Write the file**

```css
/* ── Lobby ─────────────────────────────────────────────────────────── */
.lobby-panel {
    display: flex;
    flex-direction: column;
    gap: 16px;
    align-items: center;
    width: 100%;
    max-width: 540px;
}
.lobby-self {
    text-align: center;
    color: var(--text-secondary);
    font-family: var(--font-serif);
}
.lobby-self .your-name {
    color: var(--color-gold);
    font-size: clamp(20px, 3vw, 28px);
    font-weight: 600;
    margin-top: 4px;
}
.lobby-empty {
    color: var(--text-mute);
    font-style: italic;
    text-align: center;
    margin: 24px 0;
}
.lobby-list {
    display: flex;
    flex-direction: column;
    gap: 8px;
    width: 100%;
}
.lobby-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    background: rgba(255, 255, 255, 0.03);
    border: 1px solid rgba(232, 226, 255, 0.12);
    border-radius: 4px;
    padding: 12px 16px;
}
.lobby-row .name {
    font-family: var(--font-serif);
    font-size: 18px;
    color: var(--text-primary);
}
.lobby-row .btn-challenge {
    border-color: var(--color-gold);
    padding: 6px 14px;
    min-height: 32px;
    font-size: 14px;
}

.lobby-pending {
    text-align: center;
    color: var(--color-gold);
    font-style: italic;
    padding: 24px 0;
}

/* ── Challenge prompt modal ───────────────────────────────────────── */
.challenge-modal-backdrop {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.55);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 50;
}
.challenge-modal {
    background: linear-gradient(180deg, var(--bg-mid), var(--bg-deep));
    border: 1px solid var(--color-gold);
    border-radius: 6px;
    padding: 24px;
    max-width: 360px;
    width: 90%;
    text-align: center;
    box-shadow: 0 0 32px rgba(255, 215, 107, 0.2);
}
.challenge-modal h3 {
    font-family: var(--font-serif);
    color: var(--color-gold);
    margin: 0 0 8px;
}
.challenge-modal p {
    color: var(--text-secondary);
    margin: 0 0 20px;
}
.challenge-countdown-bar {
    height: 4px;
    background: rgba(255, 215, 107, 0.15);
    border-radius: 2px;
    overflow: hidden;
    margin-bottom: 16px;
}
.challenge-countdown-bar > div {
    height: 100%;
    background: var(--color-gold);
    transition: width 1s linear;
}
.challenge-modal .menu-buttons {
    margin-top: 0;
}

/* ── Disconnect banner ─────────────────────────────────────────────── */
.disconnect-banner {
    background: rgba(255, 122, 158, 0.08);
    border: 1px solid var(--color-crimson);
    border-radius: 4px;
    padding: 8px 14px;
    color: var(--color-crimson);
    font-size: 14px;
    text-align: center;
    width: min(60vmin, 480px);
}
.disconnect-banner .countdown {
    font-family: var(--font-serif);
    font-weight: 600;
    margin-left: 6px;
}

@media (max-width: 480px) {
    .lobby-panel { max-width: none; padding: 0 16px; }
    .lobby-row { flex-direction: column; gap: 8px; align-items: stretch; }
    .lobby-row .btn-challenge { width: 100%; }
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: clean build. (Verify the `<link>` for `css/multiplayer.css` from Task 2 doesn't 404 anymore via DevTools when running the app.)

---

## Task 12: Implement `GameHub.cs`

The full SignalR endpoint. Coordinates the three singletons + speaks to clients.

**Files:**
- Create: `web-multiplayer/Hubs/GameHub.cs`
- Modify: `web-multiplayer/Program.cs` (add `AddSignalR`, map hub)

- [ ] **Step 1: Write `GameHub.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using TicTacToe.Core;
using TicTacToe.WebMultiplayer.Services;

namespace TicTacToe.WebMultiplayer.Hubs;

public class GameHub : Hub
{
    private const string LobbyGroup = "lobby";
    private const int DisconnectGraceSeconds = 30;

    private readonly PresenceTracker _presence;
    private readonly MatchRegistry _matches;
    private readonly ChallengeBroker _challenges;
    private readonly IHubContext<GameHub> _hub;

    public GameHub(
        PresenceTracker presence,
        MatchRegistry matches,
        ChallengeBroker challenges,
        IHubContext<GameHub> hub)
    {
        _presence = presence;
        _matches = matches;
        _challenges = challenges;
        _hub = hub;
    }

    // ─── Lifecycle ─────────────────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        // Presence is created on EnterLobby, not on connect — clients control when to enter the lobby.
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connId = Context.ConnectionId;
        // If they were in a match, start the disconnect grace timer.
        var matchId = _presence.CurrentMatchOf(connId);
        if (matchId is not null)
        {
            var m = _matches.Get(matchId);
            if (m is not null && m.Winner is null && !m.IsDraw)
            {
                m.DisconnectedConnId = connId;
                m.DisconnectTimer?.Dispose();
                m.DisconnectTimer = new Timer(_ => OnGraceExpired(matchId, connId),
                    null, TimeSpan.FromSeconds(DisconnectGraceSeconds), Timeout.InfiniteTimeSpan);
                var partnerConnId = m.AscendantConnId == connId ? m.VanguardConnId : m.AscendantConnId;
                await _hub.Clients.Client(partnerConnId)
                    .SendAsync("OnOpponentDisconnected", new { secondsLeft = DisconnectGraceSeconds });
            }
        }
        // Always clean lobby presence + challenges.
        _presence.Remove(connId);
        _challenges.RemoveAllInvolving(connId);
        await Clients.Group(LobbyGroup).SendAsync("OnPlayerLeft", new { connId });
        await base.OnDisconnectedAsync(exception);
    }

    // ─── Lobby ─────────────────────────────────────────────────────

    public async Task EnterLobby()
    {
        var connId = Context.ConnectionId;
        // If the player is reconnecting and was in an active match, do NOT add to lobby; let them rejoin.
        // Otherwise add fresh.
        if (_presence.Get(connId) is null)
            _presence.Add(connId);
        await Groups.AddToGroupAsync(connId, LobbyGroup);

        var me = _presence.Get(connId)!;
        var others = _presence.SnapshotLobby();
        await Clients.Caller.SendAsync("OnLobbyState", new
        {
            yourConnId = connId,
            yourName = me.Name,
            players = others
        });
        await Clients.OthersInGroup(LobbyGroup).SendAsync("OnPlayerJoined", new { connId, name = me.Name });
    }

    public async Task LeaveLobby()
    {
        var connId = Context.ConnectionId;
        await Groups.RemoveFromGroupAsync(connId, LobbyGroup);
        _presence.Remove(connId);
        _challenges.RemoveAllInvolving(connId);
        await Clients.Group(LobbyGroup).SendAsync("OnPlayerLeft", new { connId });
    }

    // ─── Challenges ────────────────────────────────────────────────

    public async Task ChallengePlayer(string targetConnectionId)
    {
        var fromConnId = Context.ConnectionId;
        var from = _presence.Get(fromConnId);
        var to = _presence.Get(targetConnectionId);
        if (from is null || to is null) return;
        // Reject if either party is already in a match.
        if (_presence.CurrentMatchOf(fromConnId) is not null || _presence.CurrentMatchOf(targetConnectionId) is not null) return;

        var c = _challenges.Create(fromConnId, from.Name, targetConnectionId, to.Name);
        await Clients.Client(targetConnectionId).SendAsync("OnIncomingChallenge", new
        {
            challengeId = c.ChallengeId,
            fromConnId,
            fromName = from.Name
        });
    }

    public async Task RespondToChallenge(string challengeId, bool accept)
    {
        var c = _challenges.Take(challengeId);
        if (c is null) return;
        if (c.ToConnId != Context.ConnectionId) return; // not your challenge

        if (!accept)
        {
            await Clients.Client(c.FromConnId).SendAsync("OnChallengeDeclined", new { fromName = c.ToName });
            return;
        }

        // Accept: create match, challenger = Ascendant.
        var match = _matches.Create(c.FromConnId, c.FromName, c.ToConnId, c.ToName);
        _presence.EnterMatch(c.FromConnId, match.MatchId);
        _presence.EnterMatch(c.ToConnId, match.MatchId);
        await Clients.Group(LobbyGroup).SendAsync("OnPlayerLeft", new { connId = c.FromConnId });
        await Clients.Group(LobbyGroup).SendAsync("OnPlayerLeft", new { connId = c.ToConnId });

        await Clients.Client(c.FromConnId).SendAsync("OnChallengeAccepted", new
        {
            matchId = match.MatchId,
            opponentName = c.ToName,
            yourFaction = "Ascendant"
        });
        await Clients.Client(c.ToConnId).SendAsync("OnChallengeAccepted", new
        {
            matchId = match.MatchId,
            opponentName = c.FromName,
            yourFaction = "Vanguard"
        });

        // Send initial match state to both.
        await BroadcastMatchState(match);
    }

    public async Task CancelChallenge(string challengeId)
    {
        var fromConnId = Context.ConnectionId;
        if (_challenges.Cancel(challengeId, fromConnId))
        {
            // No client invocation needed — the target's prompt simply expires when they try to respond.
            // We could push OnChallengeExpired, but the target's UI ignores stale prompts on response.
        }
        await Task.CompletedTask;
    }

    // ─── Match ─────────────────────────────────────────────────────

    public async Task PlaceMove(string matchId, int row, int col)
    {
        var connId = Context.ConnectionId;
        var result = _matches.TryPlace(matchId, connId, row, col);
        if (!result.Accepted || result.Match is null) return;

        await BroadcastMatchState(result.Match);

        if (result.Match.Winner is not null || result.Match.IsDraw)
        {
            await BroadcastMatchEnded(result.Match, reason: result.Match.IsDraw ? "draw" : "win");
            EndMatchAndReturnPlayersToLobby(result.Match);
        }
    }

    public async Task ForfeitMatch(string matchId)
    {
        var m = _matches.Forfeit(matchId, Context.ConnectionId);
        if (m is null) return;
        await BroadcastMatchEnded(m, reason: "forfeit");
        EndMatchAndReturnPlayersToLobby(m);
    }

    public async Task RequestRematch(string matchId)
    {
        // Simplest v1: rematch only happens when both players ask. Server tracks who asked
        // via a flag on the match. Could be on the match record itself; for v1 we keep this minimal:
        // rematch creates a fresh match with factions swapped.
        // For Task 17 we'll make this client-coordinated (each player clicks "Rematch", second click triggers).
        await Task.CompletedTask;
    }

    public async Task RejoinMatch(string matchId)
    {
        var connId = Context.ConnectionId;
        var m = _matches.Get(matchId);
        if (m is null) return;
        // Was this connection the disconnected one?
        if (m.DisconnectedConnId is null) return;

        // The new connection ID replaces the old one for whichever faction was disconnected.
        var oldConnId = m.DisconnectedConnId;
        if (m.AscendantConnId == oldConnId) m.AscendantConnId = connId;
        else if (m.VanguardConnId == oldConnId) m.VanguardConnId = connId;
        else return;

        m.DisconnectedConnId = null;
        m.DisconnectTimer?.Dispose();
        m.DisconnectTimer = null;

        _presence.EnterMatch(connId, m.MatchId);
        var partnerConnId = m.AscendantConnId == connId ? m.VanguardConnId : m.AscendantConnId;
        await Clients.Client(partnerConnId).SendAsync("OnOpponentReconnected");
        await BroadcastMatchState(m);
    }

    // ─── Helpers ───────────────────────────────────────────────────

    private async Task BroadcastMatchState(MultiplayerMatch m)
    {
        var payload = new
        {
            matchId = m.MatchId,
            board = SerializeBoard(m.Board),
            activeFaction = m.Active.ToString(),
            winner = m.Winner?.ToString(),
            isDraw = m.IsDraw,
            winningCells = m.WinningCells?.Select(c => new { c.row, c.col }).ToArray()
        };
        await _hub.Clients.Client(m.AscendantConnId).SendAsync("OnMatchState", payload);
        await _hub.Clients.Client(m.VanguardConnId).SendAsync("OnMatchState", payload);
    }

    private async Task BroadcastMatchEnded(MultiplayerMatch m, string reason)
    {
        await _hub.Clients.Client(m.AscendantConnId).SendAsync("OnMatchEnded", new
        {
            matchId = m.MatchId,
            result = m.Winner == Player.Ascendant ? "win" : (m.Winner == Player.Vanguard ? "loss" : "draw"),
            reason,
            winningCells = m.WinningCells?.Select(c => new { c.row, c.col }).ToArray()
        });
        await _hub.Clients.Client(m.VanguardConnId).SendAsync("OnMatchEnded", new
        {
            matchId = m.MatchId,
            result = m.Winner == Player.Vanguard ? "win" : (m.Winner == Player.Ascendant ? "loss" : "draw"),
            reason,
            winningCells = m.WinningCells?.Select(c => new { c.row, c.col }).ToArray()
        });
    }

    private void EndMatchAndReturnPlayersToLobby(MultiplayerMatch m)
    {
        _presence.LeaveMatch(m.AscendantConnId);
        _presence.LeaveMatch(m.VanguardConnId);
        _matches.Remove(m.MatchId);
        // Don't auto-add back to LobbyGroup; clients re-call EnterLobby on their "back to menu".
    }

    private void OnGraceExpired(string matchId, string disconnectedConnId)
    {
        var m = _matches.Get(matchId);
        if (m is null || m.DisconnectedConnId != disconnectedConnId) return;
        // Forfeit by the still-disconnected player.
        m.Winner = m.AscendantConnId == disconnectedConnId ? Player.Vanguard : Player.Ascendant;
        var partnerConnId = m.AscendantConnId == disconnectedConnId ? m.VanguardConnId : m.AscendantConnId;
        _hub.Clients.Client(partnerConnId).SendAsync("OnMatchEnded", new
        {
            matchId = m.MatchId,
            result = "win",
            reason = "forfeit",
            winningCells = (object?)null
        }).GetAwaiter().GetResult();
        EndMatchAndReturnPlayersToLobby(m);
    }

    private static int[][] SerializeBoard(Board b)
    {
        var rows = new int[3][];
        for (int r = 0; r < 3; r++)
        {
            rows[r] = new int[3];
            for (int c = 0; c < 3; c++) rows[r][c] = (int)b.At(r, c);
        }
        return rows;
    }
}
```

- [ ] **Step 2: Update `Program.cs` to add SignalR + map hub**

In `web-multiplayer/Program.cs`, add:

```csharp
using TicTacToe.WebMultiplayer.Hubs;

// In Services section:
builder.Services.AddSignalR();

// After app.MapRazorComponents(...) (or app.MapBlazorHub() in older templates):
app.MapHub<GameHub>("/gamehub");
```

- [ ] **Step 3: Wire `ChallengeBroker.Expired` to push to clients**

In `Program.cs` (after `var app = builder.Build();` and after `MapHub`):

```csharp
// Wire challenge-expiry notifications.
var hub = app.Services.GetRequiredService<IHubContext<GameHub>>();
var broker = app.Services.GetRequiredService<ChallengeBroker>();
broker.Expired += c =>
{
    hub.Clients.Client(c.FromConnId).SendAsync("OnChallengeExpired", new { challengeId = c.ChallengeId, role = "from" });
    hub.Clients.Client(c.ToConnId)  .SendAsync("OnChallengeExpired", new { challengeId = c.ChallengeId, role = "to" });
};
```

(Add `using Microsoft.AspNetCore.SignalR;` at the top if needed.)

- [ ] **Step 4: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: clean build.

---

## Task 13: Author `Lobby.razor`, `ChallengePrompt.razor`, `DisconnectBanner.razor`

These render multiplayer-specific UI but don't yet talk to the hub directly — Task 17 wires them through `Game.razor`'s SignalR client.

**Files:**
- Create: `web-multiplayer/Components/Lobby.razor`
- Create: `web-multiplayer/Components/ChallengePrompt.razor`
- Create: `web-multiplayer/Components/DisconnectBanner.razor`

- [ ] **Step 1: Write `Lobby.razor`**

```razor
<div class="lobby-panel">
    <div class="lobby-self">
        <div class="label">You are</div>
        <div class="your-name">@YourName</div>
    </div>

    @if (PendingChallengeFor is not null)
    {
        <div class="lobby-pending">
            Waiting for @PendingChallengeFor to respond…
            <button class="btn btn-link" @onclick="OnCancelPending">Cancel</button>
        </div>
    }
    else if (Players.Count == 0)
    {
        <div class="lobby-empty">No other players right now. Share the URL with someone to play.</div>
    }
    else
    {
        <div class="lobby-list">
            @foreach (var p in Players)
            {
                var capture = p;
                <div class="lobby-row">
                    <span class="name">@capture.Name</span>
                    <button class="btn btn-primary btn-challenge"
                            @onclick="@(() => OnChallenge.InvokeAsync(capture.ConnId))">
                        ⚔ Challenge
                    </button>
                </div>
            }
        </div>
    }

    <div class="menu-buttons">
        <button class="btn btn-secondary" @onclick="OnLeave">Back to menu</button>
    </div>
</div>

@code {
    public sealed record LobbyEntry(string ConnId, string Name);

    [Parameter, EditorRequired] public string YourName { get; set; } = default!;
    [Parameter] public IReadOnlyList<LobbyEntry> Players { get; set; } = Array.Empty<LobbyEntry>();
    [Parameter] public string? PendingChallengeFor { get; set; }
    [Parameter] public EventCallback<string> OnChallenge { get; set; }
    [Parameter] public EventCallback OnCancelPending { get; set; }
    [Parameter] public EventCallback OnLeave { get; set; }
}
```

- [ ] **Step 2: Write `ChallengePrompt.razor`**

```razor
@implements IDisposable

<div class="challenge-modal-backdrop">
    <div class="challenge-modal">
        <h3>@FromName wants to duel</h3>
        <p>The Ascendant moves first.</p>
        <div class="challenge-countdown-bar"><div style="width: @(_pct.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))%;"></div></div>
        <div class="menu-buttons">
            <button class="btn btn-primary" @onclick="OnAcceptInvoke">Accept</button>
            <button class="btn btn-secondary" @onclick="OnDeclineInvoke">Decline</button>
        </div>
    </div>
</div>

@code {
    [Parameter, EditorRequired] public string FromName { get; set; } = default!;
    [Parameter] public int TotalSeconds { get; set; } = 30;
    [Parameter] public EventCallback OnAccept { get; set; }
    [Parameter] public EventCallback OnDecline { get; set; }

    private System.Timers.Timer? _ticker;
    private DateTimeOffset _expiresAt;
    private double _pct = 100.0;

    protected override void OnInitialized()
    {
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(TotalSeconds);
        _ticker = new System.Timers.Timer(250);
        _ticker.Elapsed += (_, _) =>
        {
            var remaining = (_expiresAt - DateTimeOffset.UtcNow).TotalSeconds;
            _pct = Math.Max(0, (remaining / TotalSeconds) * 100.0);
            InvokeAsync(StateHasChanged);
        };
        _ticker.Start();
    }

    private async Task OnAcceptInvoke() => await OnAccept.InvokeAsync();
    private async Task OnDeclineInvoke() => await OnDecline.InvokeAsync();

    public void Dispose() { _ticker?.Stop(); _ticker?.Dispose(); }
}
```

- [ ] **Step 3: Write `DisconnectBanner.razor`**

```razor
@implements IDisposable

<div class="disconnect-banner">
    <span>@OpponentName has disconnected.</span>
    <span class="countdown">@_remaining s</span>
</div>

@code {
    [Parameter, EditorRequired] public string OpponentName { get; set; } = default!;
    [Parameter] public int InitialSeconds { get; set; } = 30;

    private System.Timers.Timer? _ticker;
    private DateTimeOffset _expiresAt;
    private int _remaining;

    protected override void OnInitialized()
    {
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(InitialSeconds);
        _remaining = InitialSeconds;
        _ticker = new System.Timers.Timer(500);
        _ticker.Elapsed += (_, _) =>
        {
            _remaining = Math.Max(0, (int)Math.Ceiling((_expiresAt - DateTimeOffset.UtcNow).TotalSeconds));
            InvokeAsync(StateHasChanged);
        };
        _ticker.Start();
    }

    public void Dispose() { _ticker?.Stop(); _ticker?.Dispose(); }
}
```

- [ ] **Step 4: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: clean build. (Components don't render anywhere yet; Task 17 wires them in.)

---

## Task 14: Add SignalR client NuGet package

The Razor component (`Game.razor`) needs to call into the SignalR hub. The Blazor Server template doesn't include the SignalR client by default — we add it explicitly so the same circuit can talk to its own hub via `HubConnection`.

**Files:**
- Modify: `web-multiplayer/TicTacToe.WebMultiplayer.csproj`

- [ ] **Step 1: Add the package**

```powershell
dotnet add web-multiplayer/TicTacToe.WebMultiplayer.csproj package Microsoft.AspNetCore.SignalR.Client
```
Expected: package adds an `<ItemGroup><PackageReference />` block with `Microsoft.AspNetCore.SignalR.Client` Version `9.x.x`.

- [ ] **Step 2: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: clean build.

---

## Task 15: Extend `Game.razor` with networking flow

This is the largest task — replaces the `NetworkPlaceholder` branch with a real lobby + match flow that talks to the hub via `HubConnection`. Local modes are unchanged.

**Files:**
- Modify: `web-multiplayer/Components/Game.razor`

- [ ] **Step 1: Replace `Game.razor` with the full network-enabled version**

```razor
@implements IAsyncDisposable
@inject LocalGameSession Local
@inject NavigationManager Nav

<div class="app-shell">
    <main class="app-main">
        @switch (_phase)
        {
            case AppPhase.MainMenu:
                <MainMenu OnHotSeat="StartHotSeat"
                          OnVsComputer="GoToModeSetup"
                          OnNetwork="GoToNetwork" />
                break;

            case AppPhase.LocalModeSetup:
                <ModeSetup OnBack="GoToMainMenu" OnStart="StartVersusComputer" />
                break;

            case AppPhase.LocalInMatch:
            case AppPhase.LocalMatchOver:
                <LocalMatchView />
                break;

            case AppPhase.NetworkConnecting:
                <div class="menu-panel">
                    <h2 class="subtitle">Connecting…</h2>
                    <div class="menu-buttons">
                        <button class="btn btn-secondary" @onclick="GoToMainMenu">Cancel</button>
                    </div>
                </div>
                break;

            case AppPhase.NetworkLobby:
                <Lobby YourName="@_yourName"
                       Players="@_lobbyPlayers"
                       PendingChallengeFor="@_pendingTargetName"
                       OnChallenge="ChallengePlayer"
                       OnCancelPending="CancelPendingChallenge"
                       OnLeave="LeaveNetwork" />
                break;

            case AppPhase.NetworkInMatch:
            case AppPhase.NetworkMatchOver:
                <div class="match-panel">
                    <div class="status-banner">
                        @if (_phase == AppPhase.NetworkMatchOver)
                        {
                            <span class="status-text status-@(_netResultClass)">@_netEndMessage</span>
                        }
                        else
                        {
                            <span class="status-text status-@FactionClass(_netActiveFaction)">
                                @(_netActiveFaction == _yourFaction
                                    ? "Your move"
                                    : $"{_opponentName}'s move…")
                            </span>
                        }
                    </div>

                    @if (_opponentDisconnected)
                    {
                        <DisconnectBanner OpponentName="@_opponentName" InitialSeconds="30" />
                    }

                    <BoardView Board="@_netBoard"
                               HoverPlayer="@_yourFaction"
                               AcceptingClicks="@(_phase == AppPhase.NetworkInMatch && _netActiveFaction == _yourFaction && !_opponentDisconnected)"
                               WinningCells="@_netWinningCells"
                               OnCellClick="OnNetCellClick" />

                    @if (_phase == AppPhase.NetworkMatchOver)
                    {
                        <div class="menu-buttons">
                            <button class="btn btn-primary" @onclick="ReturnToLobby">Back to lobby</button>
                            <button class="btn btn-secondary" @onclick="GoToMainMenu">Main menu</button>
                        </div>
                    }
                </div>
                break;
        }

        @if (_incomingChallengeId is not null)
        {
            <ChallengePrompt FromName="@_incomingChallengeFromName!"
                             TotalSeconds="30"
                             OnAccept="AcceptChallenge"
                             OnDecline="DeclineChallenge" />
        }
    </main>
</div>

@code {
    private enum AppPhase
    {
        MainMenu, LocalModeSetup, LocalInMatch, LocalMatchOver,
        NetworkConnecting, NetworkLobby, NetworkInMatch, NetworkMatchOver
    }

    private AppPhase _phase = AppPhase.MainMenu;

    // ─── Network state ─────────────────────────────────────────────
    private Microsoft.AspNetCore.SignalR.Client.HubConnection? _hub;
    private string _yourConnId = "";
    private string _yourName = "";
    private List<Lobby.LobbyEntry> _lobbyPlayers = new();
    private string? _pendingChallengeId;
    private string? _pendingTargetName;
    private string? _incomingChallengeId;
    private string? _incomingChallengeFromConnId;
    private string? _incomingChallengeFromName;
    private string? _matchId;
    private string _opponentName = "";
    private Player _yourFaction = Player.None;
    private Board _netBoard = new();
    private Player _netActiveFaction = Player.Ascendant;
    private (int row, int col)[]? _netWinningCells;
    private string? _netEndMessage;
    private string _netResultClass = "ascendant";
    private bool _opponentDisconnected;

    // ─── Lifecycle ────────────────────────────────────────────────
    protected override void OnInitialized()
    {
        Local.OnChange += HandleLocalChange;
    }

    public async ValueTask DisposeAsync()
    {
        Local.OnChange -= HandleLocalChange;
        if (_hub is not null) await _hub.DisposeAsync();
    }

    private void HandleLocalChange()
    {
        _phase = Local.Phase switch
        {
            LocalPhase.MainMenu  => AppPhase.MainMenu,
            LocalPhase.ModeSetup => AppPhase.LocalModeSetup,
            LocalPhase.InMatch   => AppPhase.LocalInMatch,
            LocalPhase.MatchOver => AppPhase.LocalMatchOver,
            _ => _phase
        };
        InvokeAsync(StateHasChanged);
    }

    // ─── Local mode handlers ──────────────────────────────────────
    private void StartHotSeat() => Local.StartHotSeat();
    private void GoToModeSetup() => Local.GoToModeSetup();
    private async Task StartVersusComputer((Player faction, Difficulty difficulty) args)
        => await Local.StartVersusComputerAsync(args.faction, args.difficulty);

    private void GoToMainMenu()
    {
        _phase = AppPhase.MainMenu;
        Local.GoToMainMenu();
    }

    // ─── Network mode handlers ────────────────────────────────────
    private async Task GoToNetwork()
    {
        _phase = AppPhase.NetworkConnecting;
        StateHasChanged();
        await EnsureHubAsync();
        await _hub!.SendAsync("EnterLobby");
    }

    private async Task LeaveNetwork()
    {
        if (_hub is not null) await _hub.SendAsync("LeaveLobby");
        await TeardownHubAsync();
        _phase = AppPhase.MainMenu;
        StateHasChanged();
    }

    private async Task EnsureHubAsync()
    {
        if (_hub is not null) return;
        var hubUrl = Nav.ToAbsoluteUri("/gamehub");
        _hub = new Microsoft.AspNetCore.SignalR.Client.HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hub.On<LobbyState>("OnLobbyState", OnLobbyState);
        _hub.On<PlayerInfo>("OnPlayerJoined", OnPlayerJoined);
        _hub.On<PlayerLeft>("OnPlayerLeft", OnPlayerLeft);
        _hub.On<IncomingChallenge>("OnIncomingChallenge", OnIncomingChallenge);
        _hub.On<ChallengeResolution>("OnChallengeAccepted", OnChallengeAccepted);
        _hub.On<DeclineMessage>("OnChallengeDeclined", OnChallengeDeclined);
        _hub.On<ChallengeExpired>("OnChallengeExpired", OnChallengeExpired);
        _hub.On<MatchState>("OnMatchState", OnMatchState);
        _hub.On<DisconnectInfo>("OnOpponentDisconnected", OnOpponentDisconnected);
        _hub.On("OnOpponentReconnected", OnOpponentReconnected);
        _hub.On<MatchEnded>("OnMatchEnded", OnMatchEnded);

        await _hub.StartAsync();
    }

    private async Task TeardownHubAsync()
    {
        if (_hub is null) return;
        await _hub.DisposeAsync();
        _hub = null;
        _yourConnId = "";
        _yourName = "";
        _lobbyPlayers.Clear();
        _pendingChallengeId = null;
        _pendingTargetName = null;
        _incomingChallengeId = null;
        _matchId = null;
    }

    private async Task ChallengePlayer(string targetConnId)
    {
        if (_hub is null) return;
        var target = _lobbyPlayers.FirstOrDefault(p => p.ConnId == targetConnId);
        if (target is null) return;
        _pendingChallengeId = "(awaiting server)";
        _pendingTargetName = target.Name;
        await _hub.SendAsync("ChallengePlayer", targetConnId);
    }

    private async Task CancelPendingChallenge()
    {
        if (_hub is null || _pendingChallengeId is null) return;
        await _hub.SendAsync("CancelChallenge", _pendingChallengeId);
        _pendingChallengeId = null;
        _pendingTargetName = null;
        StateHasChanged();
    }

    private async Task AcceptChallenge()
    {
        if (_hub is null || _incomingChallengeId is null) return;
        await _hub.SendAsync("RespondToChallenge", _incomingChallengeId, true);
        _incomingChallengeId = null;
    }

    private async Task DeclineChallenge()
    {
        if (_hub is null || _incomingChallengeId is null) return;
        await _hub.SendAsync("RespondToChallenge", _incomingChallengeId, false);
        _incomingChallengeId = null;
        StateHasChanged();
    }

    private async Task OnNetCellClick((int row, int col) move)
    {
        if (_hub is null || _matchId is null) return;
        await _hub.SendAsync("PlaceMove", _matchId, move.row, move.col);
    }

    private async Task ReturnToLobby()
    {
        _phase = AppPhase.NetworkLobby;
        _matchId = null;
        _netBoard = new Board();
        _netWinningCells = null;
        _netEndMessage = null;
        _opponentDisconnected = false;
        if (_hub is not null) await _hub.SendAsync("EnterLobby");
        StateHasChanged();
    }

    // ─── Hub callbacks ────────────────────────────────────────────
    private record LobbyState(string yourConnId, string yourName, List<PlayerInfo> players);
    private record PlayerInfo(string ConnectionId, string Name);
    private record PlayerLeft(string connId);
    private record IncomingChallenge(string challengeId, string fromConnId, string fromName);
    private record ChallengeResolution(string matchId, string opponentName, string yourFaction);
    private record DeclineMessage(string fromName);
    private record ChallengeExpired(string challengeId, string role);
    private record MatchState(string matchId, int[][] board, string activeFaction, string? winner, bool isDraw, CellRef[]? winningCells);
    private record CellRef(int row, int col);
    private record DisconnectInfo(int secondsLeft);
    private record MatchEnded(string matchId, string result, string reason, CellRef[]? winningCells);

    private void OnLobbyState(LobbyState s)
    {
        _yourConnId = s.yourConnId;
        _yourName = s.yourName;
        _lobbyPlayers = s.players
            .Where(p => p.ConnectionId != s.yourConnId)
            .Select(p => new Lobby.LobbyEntry(p.ConnectionId, p.Name))
            .ToList();
        _phase = AppPhase.NetworkLobby;
        InvokeAsync(StateHasChanged);
    }

    private void OnPlayerJoined(PlayerInfo p)
    {
        if (p.ConnectionId == _yourConnId) return;
        if (_lobbyPlayers.Any(x => x.ConnId == p.ConnectionId)) return;
        _lobbyPlayers.Add(new Lobby.LobbyEntry(p.ConnectionId, p.Name));
        InvokeAsync(StateHasChanged);
    }

    private void OnPlayerLeft(PlayerLeft p)
    {
        _lobbyPlayers.RemoveAll(x => x.ConnId == p.connId);
        InvokeAsync(StateHasChanged);
    }

    private void OnIncomingChallenge(IncomingChallenge c)
    {
        _incomingChallengeId = c.challengeId;
        _incomingChallengeFromConnId = c.fromConnId;
        _incomingChallengeFromName = c.fromName;
        InvokeAsync(StateHasChanged);
    }

    private void OnChallengeAccepted(ChallengeResolution r)
    {
        _matchId = r.matchId;
        _opponentName = r.opponentName;
        _yourFaction = r.yourFaction == "Ascendant" ? Player.Ascendant : Player.Vanguard;
        _netBoard = new Board();
        _netActiveFaction = Player.Ascendant;
        _netWinningCells = null;
        _netEndMessage = null;
        _opponentDisconnected = false;
        _phase = AppPhase.NetworkInMatch;
        // Clear any leftover challenge state.
        _pendingChallengeId = null;
        _pendingTargetName = null;
        _incomingChallengeId = null;
        InvokeAsync(StateHasChanged);
    }

    private void OnChallengeDeclined(DeclineMessage m)
    {
        _pendingChallengeId = null;
        _pendingTargetName = null;
        InvokeAsync(StateHasChanged);
    }

    private void OnChallengeExpired(ChallengeExpired e)
    {
        if (e.role == "from") { _pendingChallengeId = null; _pendingTargetName = null; }
        else if (e.role == "to") { _incomingChallengeId = null; }
        InvokeAsync(StateHasChanged);
    }

    private void OnMatchState(MatchState m)
    {
        _matchId = m.matchId;
        _netBoard = DeserializeBoard(m.board);
        _netActiveFaction = ParsePlayer(m.activeFaction);
        _netWinningCells = m.winningCells?.Select(c => (c.row, c.col)).ToArray();
        if (m.winner is not null || m.isDraw)
        {
            _phase = AppPhase.NetworkMatchOver;
            // _netEndMessage will be filled by OnMatchEnded if it follows; for now show neutral.
        }
        else
        {
            _phase = AppPhase.NetworkInMatch;
        }
        InvokeAsync(StateHasChanged);
    }

    private void OnOpponentDisconnected(DisconnectInfo d)
    {
        _opponentDisconnected = true;
        InvokeAsync(StateHasChanged);
    }

    private void OnOpponentReconnected()
    {
        _opponentDisconnected = false;
        InvokeAsync(StateHasChanged);
    }

    private void OnMatchEnded(MatchEnded e)
    {
        _phase = AppPhase.NetworkMatchOver;
        _netEndMessage = (e.result, e.reason) switch
        {
            ("win", "forfeit") => $"{_opponentName} forfeited — you win.",
            ("loss", "forfeit") => $"You forfeited — {_opponentName} wins.",
            ("win", _)  => $"You hold the line. Victory.",
            ("loss", _) => $"{_opponentName} holds the line.",
            ("draw", _) => "Stalemate. The galaxy holds its breath.",
            _ => "Match ended."
        };
        _netResultClass = e.result switch
        {
            "win" => _yourFaction == Player.Ascendant ? "ascendant" : "vanguard",
            "loss" => _yourFaction == Player.Ascendant ? "vanguard" : "ascendant",
            _ => "none"
        };
        _netWinningCells = e.winningCells?.Select(c => (c.row, c.col)).ToArray();
        InvokeAsync(StateHasChanged);
    }

    // ─── Helpers ──────────────────────────────────────────────────
    private static Board DeserializeBoard(int[][] cells)
    {
        var b = new Board();
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                if (cells[r][c] != 0)
                    b.TryPlace(r, c, (Player)cells[r][c]);
        return b;
    }

    private static Player ParsePlayer(string s) => s switch
    {
        "Ascendant" => Player.Ascendant,
        "Vanguard"  => Player.Vanguard,
        _           => Player.None
    };

    private static string FactionClass(Player p) => p switch
    {
        Player.Ascendant => "ascendant",
        Player.Vanguard  => "vanguard",
        _                => "none"
    };

    // Simple inline component to avoid duplicating the local-match panel above.
    private RenderFragment LocalMatchView() => __builder =>
    {
        <div class="match-panel">
            <div class="status-banner">
                @if (_phase == AppPhase.LocalMatchOver)
                {
                    @if (Local.Winner is { } w)
                    {
                        <span class="status-text status-@FactionClass(w)">@ThemeText.WinMessage(w)</span>
                    }
                    else if (Local.IsDraw)
                    {
                        <span class="status-text">@ThemeText.DrawMessage()</span>
                    }
                }
                else if (Local.AiThinking)
                {
                    <span class="status-text status-@FactionClass(Local.Active) status-thinking">
                        @ThemeText.NameOf(Local.Active) considers the grid…
                    </span>
                }
                else
                {
                    <span class="status-text status-@FactionClass(Local.Active)">
                        @ThemeText.NameOf(Local.Active) — claim a sector
                    </span>
                }
            </div>

            <BoardView Board="Local.Board"
                       HoverPlayer="@(Local.VersusComputer ? Local.HumanSide : Local.Active)"
                       AcceptingClicks="@(Local.Phase == LocalPhase.InMatch && !Local.AiThinking && (!Local.VersusComputer || Local.Active == Local.HumanSide))"
                       WinningCells="Local.WinningCells"
                       OnCellClick="@(async ((int r, int c) m) => await Local.TryHumanMoveAsync(m.r, m.c))" />

            <ScoreBoard AscendantWins="Local.AscendantWins"
                        VanguardWins="Local.VanguardWins"
                        Draws="Local.Draws"
                        ShowReset="true"
                        OnReset="@(() => Local.ResetScores())" />

            @if (_phase == AppPhase.LocalMatchOver)
            {
                <div class="menu-buttons">
                    <button class="btn btn-primary" @onclick="@(async () => await Local.PlayAgainAsync())">Play again</button>
                    <button class="btn btn-secondary" @onclick="GoToMainMenu">Main menu</button>
                </div>
            }
        </div>
    };
}
```

(Razor: the `LocalMatchView` helper is implemented as a method-returning RenderFragment in the same file. This pattern works inside a single component.)

- [ ] **Step 2: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: clean build. If the inline `RenderFragment` syntax doesn't compile in your Razor version, extract `LocalMatchView` into a separate `LocalMatchPanel.razor` component file with the same markup; the Game.razor switch-case then renders `<LocalMatchPanel />`.

---

## Task 16: Author the deploy artifacts

**Files:**
- Create: `web-multiplayer/deploy/deploy.sh`
- Create: `web-multiplayer/deploy/tictactoe-multi.service.template`
- Create: `web-multiplayer/README.md`

- [ ] **Step 1: Write `deploy.sh`**

```bash
#!/usr/bin/env bash
# Generic deploy script for the TicTacToe network-multiplayer service.
# All paths are configurable via environment variables. Run on the host
# that will serve the app (typically a small Linux VM/container).
#
# Usage:
#   INSTALL_DIR=/opt/tictactoe-multi ./deploy.sh
#
# Defaults:
#   INSTALL_DIR=/opt/tictactoe-multi
#   REPO_DIR=$INSTALL_DIR/repo
#   PUBLISH_DIR=$INSTALL_DIR/publish
#   SERVICE_NAME=tictactoe-multi   (matches systemd unit)
#
# Prerequisites:
#   - .NET 9 SDK installed (apt: dotnet-sdk-9.0)
#   - git checkout at $REPO_DIR with the desired branch
#   - systemd unit installed (see tictactoe-multi.service.template)
#
set -euo pipefail
: "${INSTALL_DIR:=/opt/tictactoe-multi}"
: "${REPO_DIR:=${INSTALL_DIR}/repo}"
: "${PUBLISH_DIR:=${INSTALL_DIR}/publish}"
: "${SERVICE_NAME:=tictactoe-multi}"

cd "$REPO_DIR"
git pull --ff-only
dotnet publish web-multiplayer -c Release -o "${PUBLISH_DIR}.next"
rm -rf "$PUBLISH_DIR"
mv "${PUBLISH_DIR}.next" "$PUBLISH_DIR"
sudo systemctl restart "$SERVICE_NAME"
echo "deployed: $(git rev-parse --short HEAD)"
```

Make executable:
```powershell
# On Windows you can't chmod, but git stores file mode. Add the +x bit via:
git update-index --chmod=+x web-multiplayer/deploy/deploy.sh
```

(If running on Linux/Mac, `chmod +x web-multiplayer/deploy/deploy.sh` instead.)

- [ ] **Step 2: Write `tictactoe-multi.service.template`**

```ini
[Unit]
Description=Tic-Tac-Toe Network Multiplayer
After=network.target

[Service]
Type=simple
User={{SERVICE_USER}}
WorkingDirectory={{PUBLISH_DIR}}
ExecStart=/usr/bin/dotnet {{PUBLISH_DIR}}/TicTacToe.WebMultiplayer.dll --urls {{LISTEN_URL}}
Environment=DOTNET_ENVIRONMENT=Production
Restart=on-failure
RestartSec=5
NoNewPrivileges=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths={{INSTALL_DIR}}

[Install]
WantedBy=multi-user.target
```

- [ ] **Step 3: Write `web-multiplayer/README.md`**

```markdown
# Tic-Tac-Toe Network Multiplayer

A Blazor Server app that adds three play modes to the Order vs Rebellion theme:
hot-seat, vs CPU (three difficulty tiers), and network multiplayer with
auto-named lobby + challenge UX.

## Run locally

```bash
dotnet run --project web-multiplayer --urls http://localhost:5050
```

Open http://localhost:5050 in two browser windows to test multiplayer.

## Deploy to a Linux host

The repo ships with a generic deploy script and systemd unit template.
Operator-specific bits (hostname, IP, reverse-proxy config) are intentionally
out of scope — bring your own.

### Prerequisites on the host

- .NET 9 SDK: `apt install dotnet-sdk-9.0`
- git
- A reverse proxy in front for TLS + WebSocket forwarding (Caddy, nginx,
  Traefik). The app binds to 127.0.0.1 by default and trusts the proxy.

### Initial setup

```bash
# As root:
useradd --system --home /opt/tictactoe-multi --shell /usr/sbin/nologin tictactoe
mkdir -p /opt/tictactoe-multi
chown tictactoe:tictactoe /opt/tictactoe-multi

# Clone the repo (use your own remote URL):
sudo -u tictactoe git clone <repo-url> /opt/tictactoe-multi/repo

# Install systemd unit (substitute placeholders):
sed -e 's|{{SERVICE_USER}}|tictactoe|g' \
    -e 's|{{INSTALL_DIR}}|/opt/tictactoe-multi|g' \
    -e 's|{{PUBLISH_DIR}}|/opt/tictactoe-multi/publish|g' \
    -e 's|{{LISTEN_URL}}|http://127.0.0.1:5000|g' \
    /opt/tictactoe-multi/repo/web-multiplayer/deploy/tictactoe-multi.service.template \
    > /etc/systemd/system/tictactoe-multi.service

systemctl daemon-reload
systemctl enable tictactoe-multi
```

### Each redeploy

```bash
sudo -u tictactoe /opt/tictactoe-multi/repo/web-multiplayer/deploy/deploy.sh
```

The script does `git pull`, `dotnet publish`, atomic-swap, and `systemctl restart`.

### Reverse proxy

The app speaks plain HTTP and WebSocket on whatever `--urls` it was started
with. Configure your reverse proxy to forward to it; SignalR's WebSocket
upgrade will work transparently with Caddy and Traefik. Nginx needs explicit
`Upgrade` and `Connection` headers — see the SignalR docs.
```

- [ ] **Step 4: Build**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: clean build (these are non-source artifacts).

---

## Task 17: Update root `README.md` to describe three apps

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Replace the apps table**

Open the root `README.md`. Replace the existing two-column "Console / Web" table with a three-column version, and add a third `dotnet run` invocation:

```markdown
# Tic-Tac-Toe — Order vs Rebellion

A space-opera-themed tic-tac-toe in three flavours: a colorful terminal version, an installable single-player web app, and a Blazor Server multiplayer app.
All three share a single C# game engine.

| | Console | Web (single-player) | Web Multiplayer |
|---|---|---|---|
| What it is | An ANSI-colored terminal game. | A Blazor WebAssembly Progressive Web App. Installable on desktop & mobile. Plays offline. | A Blazor Server app: hot-seat + vs CPU + auto-named multiplayer lobby. |
| Where it runs | Anywhere a .NET 9 console can run. | Any modern browser; or installed as a PWA. | Any Linux/Mac/Windows host with .NET 9; behind your reverse proxy of choice. |
| Best for | Quick game in a terminal. | Solo play, mobile-friendly, offline-capable. | Playing with someone on another device. |

The theme is **The Ascendant ☼ vs The Crimson Vanguard ✦** — original names and symbols, no trademarked content.
Three AI difficulty tiers: **Recruit** (random) / **Captain** (one-ply tactical) / **Warlord** (perfect minimax — never loses).

## Quick start

You need **.NET 9 SDK** ([download](https://dotnet.microsoft.com/download)) and that's it. The repo has no other native or system dependencies.

\`\`\`bash
git clone <this-repo>
cd tic-tac-toe-game

# play in the terminal
dotnet run --project console

# play in a browser, single-player (PWA)
dotnet run --project web

# multiplayer + local modes (Blazor Server)
dotnet run --project web-multiplayer
# → open http://localhost:5050 in two windows to test multiplayer
\`\`\`
```

(Use real triple-backticks in the file; the `\`\`\`` here is escape syntax for this plan.)

- [ ] **Step 2: Update the repo layout block**

In the same README, replace the layout block to include `web-multiplayer/`:

```text
tic-tac-toe-game/
├── core/                    Shared C# class library
├── console/                 Terminal UI
├── web/                     Single-player Blazor WASM PWA
├── web-multiplayer/         Multiplayer Blazor Server (hot-seat + vs CPU + network)
├── tools/icon-gen/          PWA icon generator
├── docs/superpowers/        Design specs, plans, screenshots
└── TicTacToe.sln
```

- [ ] **Step 3: Build (no code change but verify nothing else regressed)**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: clean build.

---

## Task 18: Manual smoke checklist

The acceptance gate. Each item must pass.

- [ ] **Step 1: Console regression**

```powershell
pwsh -File console/tests/smoke.ps1
```
Expected: `10 / 10 scenarios passed.`

- [ ] **Step 2: Build all four projects**

```powershell
dotnet build TicTacToe.sln --nologo
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 3: Run web-multiplayer locally**

```powershell
dotnet run --project web-multiplayer --urls http://localhost:5050
```

In a browser at http://localhost:5050, run through:

- [ ] **Step 4: Hot-seat regression** — same row/col/diagonal/draw behavior as `web/`. Cosmic theme renders.

- [ ] **Step 5: Vs CPU regression** — Recruit/Captain/Warlord work. Warlord doesn't lose.

- [ ] **Step 6: Two-window multiplayer match** — open two private/incognito windows. Both land in lobby with different cosmic names. A challenges B → B accepts → match plays out → win/draw correctly detected.

- [ ] **Step 7: Decline** — A challenges B → B declines → A returns to lobby with no error.

- [ ] **Step 8: Challenge timeout** — A challenges B → wait 30s → both sides see expiry, A returns to lobby.

- [ ] **Step 9: Cancel** — A challenges B → A cancels before B responds → A returns to lobby; B's prompt vanishes within a few seconds (next sweep).

- [ ] **Step 10: Disconnect mid-match** — start a match between A and B → close A's window → B sees "<name> has disconnected (Ns)…" countdown → at 0, B sees "forfeited — you win."

- [ ] **Step 11: Reconnect mid-match** — start a match → close A's window → reopen quickly (within 30s) → on the new window, click "Network multiplayer" → match should be resumable. (Note: per spec, sessionStorage clears when the tab closes, so for v1 a closed-tab reconnect is best-effort. Refreshing the tab without closing it is the supported path; verify *that* works.)

- [ ] **Step 12: Empty lobby** — open a single window → "no other players" message.

- [ ] **Step 13: Multi-window lobby** — open 5 windows → each sees the other 4 → challenge between any pair, others remain available; when match starts, the two participants disappear from others' lobbies.

- [ ] **Step 14: Server restart resilience** — start a match → restart `dotnet run` → both clients see SignalR disconnect → reload → land in main menu cleanly.

- [ ] **Step 15: Stop the server**

`Ctrl+C` in the terminal where `dotnet run` is running.

---

## Self-Review Checklist (run by the plan author after writing all tasks)

- [x] **Spec coverage:** Every spec section has a task. Project layout → Tasks 1, 2. Shared CSS/sigils → Task 2. State machine + components → Tasks 7-10, 13, 15. Services → Tasks 3-6. Hub contract → Task 12. Disconnect flow → Tasks 12, 13. Name generator → Task 3. Reference deployment → Task 16. Manual checklist → Task 18.
- [x] **Placeholder scan:** No TBD/TODO. `RequestRematch` is intentionally a stub in v1 (clients return to lobby and re-challenge); the hub method exists as a placeholder for a future task. `LocalMatchView()` is inline in Game.razor — tasks note the fallback if the inline RenderFragment shape doesn't compile.
- [x] **Type consistency:** `Player`, `Difficulty`, `Board`, `MultiplayerMatch`, `Challenge`, `PlayerSession` used identically in every task that references them. Hub method names match between `GameHub.cs` (Task 12) and Razor `_hub.SendAsync` calls (Task 15).
- [x] **Build sequencing:** Every task ends with `dotnet build`. Tasks 11-13 produce intermediate state (UI components without backing logic, hub without UI clients) but build cleanly. Task 15 closes the loop with the SignalR client wiring.

---

## Execution

Plan complete. Ready to execute.
