# Tic-Tac-Toe Network Multiplayer — `web-multiplayer/`

**Date:** 2026-05-07
**Author:** dderbidge
**Status:** Design (pre-implementation)
**Builds on:**
- `console/docs/superpowers/specs/2026-05-05-tic-tac-toe-design.md` (console version)
- `docs/superpowers/specs/2026-05-06-tic-tac-toe-web-design.md` (single-player web PWA)

## Overview

A third companion app under `web-multiplayer/` that adds two-player network play to the existing Order vs Rebellion tic-tac-toe game, while preserving the original single-player capabilities. Built as a Blazor Server app so all UI rendering and game state stays server-authoritative; SignalR handles the realtime channel between the server and each connected browser. Players are auto-assigned cosmic-themed names on arrival (no accounts), see a lobby of who else is online, and challenge each other directly. The new app reuses the `core/` class library unchanged — `Board`, `Ai`, and `ThemeText` plug into the multiplayer match flow exactly as they do in the single-player version.

## Goals & Non-Goals

**Goals**
- Three play modes in one app: hot-seat, vs-computer (with the same three difficulty tiers), and network multiplayer.
- Auto-generated, family-safe display names for network players — no accounts, no name input.
- Lobby with presence (who's online) + direct challenge UX (click a name, opponent gets accept/decline).
- Server-authoritative game state — clients never trust each other; the server validates every move via `core/Board`.
- Reconnect-tolerant: if a player drops mid-match, they have a 30-second grace period to return.
- Reusable shared codebase — `core/` is touched zero times; `web/` (single-player PWA) is unaffected.
- Generic, public-shareable repo — no homelab-specific hostnames, IPs, or container IDs in source. Operator-specific bits live in private notes outside the repo.

**Non-Goals**
- Accounts, persistent stats, leaderboards.
- In-game chat (kid-safety + content moderation overhead).
- Spectators.
- Match resumption across server restarts (in-memory only — restart abandons active matches).
- Anti-cheat beyond server-authoritative validation.
- Internet-public deployment with rate-limiting/auth (left to the operator).

## Project Layout

A new sibling folder. The other folders are not modified by this work.

```
tic-tac-toe-game/
├── core/                  (unchanged)
├── console/               (unchanged)
├── web/                   (single-player PWA, unchanged)
└── web-multiplayer/       NEW — Blazor Server app
    ├── TicTacToe.WebMultiplayer.csproj
    ├── Program.cs
    ├── App.razor, _Imports.razor, MainLayout.razor
    ├── Pages/
    │   ├── _Host.cshtml
    │   └── Index.razor
    ├── Components/
    │   ├── Game.razor                   (root, mode router)
    │   ├── MainMenu.razor               (3 buttons: hot-seat / vs CPU / network)
    │   ├── ModeSetup.razor              (vs-CPU faction + difficulty pick)
    │   ├── BoardView.razor              (clickable 3×3 grid)
    │   ├── Cell.razor
    │   ├── ScoreBoard.razor
    │   ├── Lobby.razor                  (your name, online players, challenge buttons)
    │   ├── ChallengePrompt.razor        ("X wants to play. Accept / Decline")
    │   └── DisconnectBanner.razor       ("X disconnected (24s)")
    ├── Hubs/
    │   └── GameHub.cs                   (SignalR endpoint; mediates between services)
    ├── Services/
    │   ├── LocalGameSession.cs          (hot-seat + vs CPU — local-only state)
    │   ├── PresenceTracker.cs           (singleton; who's online)
    │   ├── NameGenerator.cs             (deterministic clean name from a connection ID)
    │   ├── MatchRegistry.cs             (singleton; active multiplayer matches)
    │   └── ChallengeBroker.cs           (singleton; pending challenges with TTL)
    ├── deploy/
    │   ├── deploy.sh                    (parameterized via env vars)
    │   └── tictactoe-multi.service.template   (systemd unit with placeholders)
    └── wwwroot/
        ├── css/                         (theme.css, board.css, animations.css — copied + adapted from web/)
        └── img/                         (sigils + favicon — copied from web/)
```

## Architecture

### Tech stack

- **Blazor Server** (.NET 9) — UI components render server-side, the browser holds a SignalR connection that streams DOM diffs and event callbacks. Native to ASP.NET Core; SignalR plumbing comes for free. Single deployable.
- **SignalR** — both for Blazor's built-in render channel *and* for the explicit game/lobby hub at `/gamehub`.
- **No database.** Match state, presence, challenges, names are all in-memory singletons. Server restart = active matches forfeit; that's acceptable for a turn-based game with sub-minute matches.
- **Reverse proxy in front (recommended).** The app binds to a local HTTP port; TLS termination and SignalR/WebSocket forwarding handled by an external reverse proxy (Caddy, nginx, Traefik — operator's choice). The repo's reference deploy assumes such a proxy exists.

### Why Blazor Server (vs. Blazor WebAssembly + SignalR client)

Blazor Server is a near-perfect fit for this kind of stateful multi-user app: every client's view is reconstructed from server state on each render, so "what does Bob see right now" is just "look at Bob's component instance." With Blazor WASM, the same problem becomes "synchronize this state in two places consistently." The downside (open websocket per player) is irrelevant at this scale. The further downside (no offline mode) doesn't matter — multiplayer is meaningless offline.

### State machine

Same shape as the single-player version, with a new `Network*` family of phases for multiplayer:

```
                 ┌────────────────────────────────────────┐
                 │              MainMenu                  │
                 │  • Hot-seat                            │
                 │  • Vs Computer                         │
                 │  • Network multiplayer                 │
                 └────┬───────────────┬──────────────────┬┘
                      │               │                  │
                      │               │                  │
                Hot-seat        Vs Computer          Network
                      │               │                  │
                      │          ModeSetup               │
                      │           (faction +             │
                      │           difficulty)            │
                      │               │                  │
                      ▼               ▼                  ▼
                ┌─────────────────────────┐    ┌──────────────────┐
                │     LocalInMatch        │    │   NetworkLobby   │
                │  (hot-seat or vs CPU,   │    │  (presence list, │
                │   server-side state in  │    │   challenges)    │
                │   LocalGameSession)     │    │                  │
                └────────────┬────────────┘    └────────┬─────────┘
                             │                          │
                             ▼                          ▼
                   LocalMatchOver           ┌──────────────────────┐
                             │              │  NetworkChallenged   │
                             │              │   (waiting for       │
                             │              │   accept/decline)    │
                             │              └──────────┬───────────┘
                             │                         │
                             │              ┌──────────▼──────────┐
                             │              │  NetworkInMatch     │
                             │              │   (server-mediated, │
                             │              │   MatchRegistry)    │
                             │              └──────────┬──────────┘
                             │                         │
                             │                         ▼
                             │              ┌─────────────────────┐
                             │              │  NetworkMatchOver   │
                             │              └─────────────────────┘
                             ▼                         │
                       (back to MainMenu)              ▼
                                            (rematch | back to Lobby)
```

### SignalR hub contract (`Hubs/GameHub.cs`)

**Client → Server methods**

```csharp
Task EnterLobby();                                    // join lobby group, get name
Task LeaveLobby();                                    // back-to-menu / tab close
Task ChallengePlayer(string targetConnectionId);     // initiate challenge
Task RespondToChallenge(string challengeId, bool accept);
Task PlaceMove(string matchId, int row, int col);
Task ForfeitMatch(string matchId);
Task RequestRematch(string matchId);                 // both sides request → swap factions, new match
Task RejoinMatch(string matchId);                    // post-reconnect
```

**Server → Client invocations**

```text
OnLobbyState({yourConnId, yourName, players: [{connId, name}, ...]})
OnPlayerJoined({connId, name})
OnPlayerLeft({connId})
OnIncomingChallenge({challengeId, fromConnId, fromName})
OnChallengeAccepted({matchId, opponentName, yourFaction})
OnChallengeDeclined({fromName})
OnChallengeExpired({challengeId})
OnMatchState({board, activeFaction, winner?, winningCells?})
OnOpponentDisconnected({secondsLeft})
OnOpponentReconnected()
OnMatchEnded({result: "win"|"loss"|"draw"|"forfeit", winningCells?})
```

### Services

| Service | Lifetime | Responsibility |
|---|---|---|
| `PresenceTracker` | singleton | `Dictionary<connectionId, PlayerSession>`. Add/remove on hub `OnConnectedAsync`/`OnDisconnectedAsync`. Each session has `name`, `connectedAt`, `currentMatchId?`. |
| `NameGenerator` | static | Pure function: `string For(string connectionId)` → deterministic name from a SHA-256 hash mod adjective×noun lists. ~1,600 unique combos. Collisions get a numeric suffix. |
| `MatchRegistry` | singleton | `Dictionary<matchId, MultiplayerMatch>`. Each match holds the `Board`, `ascendantConnId`, `vanguardConnId`, `lastActivityAt`, optional `disconnectTimer`. |
| `ChallengeBroker` | singleton | `Dictionary<challengeId, Challenge>` with TTL. A timer scans every 5 s for expired challenges and fires `OnChallengeExpired` to both parties. |
| `LocalGameSession` | scoped | Per-circuit state for hot-seat/vs-CPU modes. Mirrors the v2 web app's `GameSession` design. No SignalR involvement. |

All four singletons share their internal `Dictionary` access via a single `lock`. For the expected scale (handful of concurrent matches), `ConcurrentDictionary` is unnecessary and would obscure the simple synchronous flows.

### Game state authority

Every move flows through `MatchRegistry.PlaceMove(matchId, fromConnId, row, col)`, which:

1. Locks the match.
2. Validates: match exists, `fromConnId` is one of the two participants, it's that participant's faction's turn, the cell is empty, the game isn't over.
3. Calls `core/Board.TryPlace(...)` — same code path the console and web apps use.
4. Checks for win/draw via `core/Board.Winner()` / `IsDraw()`.
5. Computes winning cells if any.
6. Returns the new state to the hub, which broadcasts `OnMatchState` to both participants.

A malicious or out-of-sync client cannot place a move on the wrong turn or in an occupied cell — the server simply ignores invalid invocations.

### Disconnect handling

1. SignalR `OnDisconnectedAsync` fires for one player mid-match.
2. Hub sends `OnOpponentDisconnected{secondsLeft: 30}` to the partner.
3. `MatchRegistry` starts a 30-second timer keyed by match id.
4. If the dropped player reconnects (new SignalR connection from the same browser, with the prior `matchId` cached in `sessionStorage`), the new connection calls `EnterLobby()` then `RejoinMatch(matchId)`. Hub: cancels the timer, swaps in the new connection ID, sends `OnOpponentReconnected` to the partner and `OnMatchState` (current snapshot) to the rejoiner.
5. If the timer fires: `OnMatchEnded{result: "win", reason: "forfeit"}` to the partner, `OnMatchEnded{result: "loss", reason: "forfeit"}` would be sent to the disconnected client when/if they reconnect (after the match is gone, they're routed straight to the lobby with a "your previous match was forfeited" toast).

`sessionStorage` is used (not `localStorage`) so closing the tab clears the rejoin state — exactly the desired behavior.

### Matchmaking

- **Auto-assigned name** on lobby entry. Names are deterministic from the SignalR connection ID, so reconnects within a session keep the same name.
- **Lobby view** lists all players currently in the lobby (i.e., not in a match), with a "Challenge" button next to each.
- **Challenge expires** after 30 seconds of no response.
- **Pending state**: a player who has issued a challenge sees a "Waiting for X to respond…" overlay; they can cancel.
- **Both-in-match exclusion**: when a match starts, both participants are removed from the lobby's broadcast set. They reappear in the lobby on `BackToLobby` after the match.
- **First-come-first-served** for simultaneous challenges to the same player. Subsequent challenges to a busy player are rejected immediately with a "X is no longer available" toast.

### Faction assignment

- Challenger plays Ascendant ☼ (gold, moves first).
- On rematch: factions swap. The player who was Vanguard plays Ascendant in the second match.

## Visual Theme

Inherits the Cosmic Sigils direction from the single-player web app:

- Same color palette (`--bg-deep: #0a0518`, gold + crimson sigils, serif headings).
- Same `theme.css` / `board.css` / `animations.css` files copied into `web-multiplayer/wwwroot/css/`. Two new component-scoped CSS additions:
  - `Lobby.razor.css` — player list as a vertical stack of cards, each with the player's auto-name in serif type and a "⚔ Challenge" button.
  - `ChallengePrompt.razor.css` — modal with "**Quiet Comet** wants to duel. Accept / Decline" — countdown bar showing remaining response time.
- Status banner during a match adds an opponent presence indicator (subtle dot, dimmed during `OnOpponentDisconnected`).

## Name Generator

```csharp
namespace TicTacToe.WebMultiplayer.Services;

public static class NameGenerator
{
    private static readonly string[] Adjectives = {
        "Quiet", "Bold", "Crimson", "Azure", "Golden", "Silver", "Distant",
        "Brave", "Swift", "Cosmic", "Nebula", "Stellar", "Lunar", "Solar",
        "Astral", "Ember", "Frost", "Twilight", "Dawn", "Ancient",
        "Bright", "Hidden", "Steady", "Restless", "Patient", "Wandering",
        "Daring", "Gentle", "Fierce", "Clever", "Watchful", "Eager",
        "Noble", "Quick", "Steep", "Vast", "Polar", "Tidal", "Velvet", "Iron",
    };
    private static readonly string[] Nouns = {
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

40 × 40 = 1,600 unique combinations. `PresenceTracker` resolves duplicates by appending a numeric suffix (`"Quiet Comet 2"`, `"Quiet Comet 3"`).

The word lists are hand-curated for family-friendliness — no slurs, no sexual/violent terms, nothing that combines into anything embarrassing. Both lists are reviewed by the operator before deploying.

## Reference Deployment

The repo describes a *reference* deployment that any operator can adapt. Operator-specific details (hostname, container ID, IP, reverse-proxy config) are intentionally **not** in the repo — they belong in the operator's own infrastructure notes.

### Reference shape

- **Host:** any Linux machine with .NET 9 SDK installed (Debian 12 / Ubuntu 24.04 tested; `apt install dotnet-sdk-9.0`).
- **Service user:** dedicated unprivileged user (e.g., `tictactoe`).
- **Install paths:** configurable via env vars in the deploy script:
  - `INSTALL_DIR` — root for service files (default `/opt/tictactoe-multi`)
  - `REPO_DIR` — git checkout (default `${INSTALL_DIR}/repo`)
  - `PUBLISH_DIR` — current build artifact (default `${INSTALL_DIR}/publish`)
  - `SERVICE_USER` — runtime user (default `tictactoe`)
  - `LISTEN_URL` — bind address (default `http://127.0.0.1:5000` so a fronting reverse proxy is required)
- **Process manager:** `systemd` unit (template at `web-multiplayer/deploy/tictactoe-multi.service.template`).
- **Reverse proxy:** out of scope — operator brings their own (Caddy/nginx/Traefik). The reference template uses `127.0.0.1:5000` so external access is gated by whatever the operator wires up.
- **TLS:** terminated by the reverse proxy. SignalR uses the same TLS connection (WebSocket Secure) — no extra config needed if the proxy is configured to forward WebSockets (Caddy and Traefik do this by default; nginx needs `Upgrade`/`Connection` headers).

### Deploy script

`web-multiplayer/deploy/deploy.sh` — operator-agnostic, env-var-driven:

```bash
#!/usr/bin/env bash
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

Runs from a git checkout; safe (atomic-ish swap, original publish kept on failure).

### Systemd unit template

`web-multiplayer/deploy/tictactoe-multi.service.template`:

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

Operator substitutes `{{...}}` placeholders with their own values during initial setup. The README documents this.

## Error Handling

- **Invalid hub invocations** (move out of turn, wrong match, occupied cell, malformed payload) → ignored silently with a server log entry. The client UI prevents these via state guards; defense-in-depth on the server.
- **Lost SignalR connection mid-match** → 30-second grace, then forfeit (see Disconnect handling).
- **Server restart with active matches** → all matches lost. Clients see SignalR disconnect, reconnect to an empty lobby. A "your previous match ended due to server restart" toast on first reconnect after restart.
- **Empty lobby** → "No other players right now. Share `<your URL>` with someone to start a game."
- **Reverse proxy down** → app unreachable; outside the app's ability to handle.
- **Concurrent challenge to busy player** → "X is no longer available" toast to the late challenger.

## Testing Strategy

The console smoke checklist (`console/tests/smoke.ps1`) keeps passing 10/10 — it tests `core/`, which this work doesn't change.

For `web-multiplayer/` v1, no automated tests (matching the rest of the codebase). A manual smoke checklist in the spec covers:

1. **Build:** `dotnet build TicTacToe.sln` — 0 warnings, 0 errors.
2. **Local run:** `dotnet run --project web-multiplayer` — opens at `http://localhost:5000`.
3. **Hot-seat regression:** play a row-win, column-win, diagonal-win, draw — same behavior as `web/`.
4. **Vs CPU regression:** play vs Recruit / Captain / Warlord — same behavior as `web/`. Warlord still never loses.
5. **Two-window network match:** open two browser tabs (or two browsers) → both land in lobby with different names → see each other → A challenges B → B accepts → match plays out → win/draw correctly detected → score updates → rematch available.
6. **Decline:** A challenges B → B declines → A sees decline, stays in lobby.
7. **Challenge timeout:** A challenges B → B doesn't respond for 30 s → both see expiry.
8. **Cancel:** A challenges B → A cancels before B responds → A returns to lobby; B's prompt vanishes silently.
9. **Disconnect mid-match:** start a match, close one tab → other tab sees disconnect banner counting down → forfeit fires at 30 s.
10. **Reconnect mid-match:** start a match, close one tab → reopen quickly → rejoiner sees current board → opponent sees "reconnected" → match continues.
11. **Empty lobby:** open a single window → see "no other players" message.
12. **Multiple lobby members:** open 5 windows → each sees the other 4 → challenges between any pair work; non-participants see them removed from the lobby for the duration of the match.
13. **Server restart:** start a match, restart the service → both clients see disconnect → reload → land in lobby cleanly.

## Decisions Locked

| # | Decision | Source |
|---|---|---|
| 1 | Reach: LAN + Tailnet (operator-specific reverse-proxy concern, not in repo) | user picked option D |
| 2 | Tech stack: Blazor Server | user picked A |
| 3 | All three play modes preserved (hot-seat, vs CPU, network) | user request |
| 4 | Auto-generated cosmic-themed names; no name input | user request |
| 5 | Server-authoritative game state | architectural decision |
| 6 | In-memory only, no DB | scope decision (turn-based, sub-minute matches) |
| 7 | 30-second disconnect grace | reasonable default, easily adjustable |
| 8 | Challenger plays Ascendant; factions swap on rematch | reasonable default |
| 9 | No chat, no spectators, no accounts in v1 | YAGNI / kid-safety |
| 10 | New sibling LXC for the operator's deployment (not the existing CT142) | architectural; kept out of public repo |
| 11 | Public-shareable repo: no homelab-specific details in source | user request |

## Out of Scope (deferred)

- In-game chat
- Spectator mode
- Account system / persistent stats / Elo
- Match resumption across server restarts
- Public-internet deployment with rate-limiting/auth
- Mobile-native app (browser PWA-on-mobile via the existing single-player satisfies the casual-mobile use case)
- AI player in network matches (vs-CPU is local-only by design)

## File Inventory at Completion

```
tic-tac-toe-game/
├── docs/superpowers/specs/2026-05-07-tic-tac-toe-network-design.md   (this file)
├── docs/superpowers/plans/2026-05-07-tic-tac-toe-network-plan.md     (next step)
├── core/                  (unchanged)
├── console/               (unchanged)
├── web/                   (unchanged)
└── web-multiplayer/       (NEW Blazor Server app, ~25 source files)
```

Operator-specific deployment details (LXC ID, IP, hostname, reverse-proxy config, AdGuard rewrite) live outside the repo in the operator's private infrastructure notes.
