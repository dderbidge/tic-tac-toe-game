# Reproducible gameplay playtesters

`tests/TicTacToe.WebMultiplayer.Tests/GameplayPlaytesterTests.cs` runs a finite
matrix of software players against the real local session state machine and a
real loopback Kestrel/SignalR server. The agents are test code, separate from the
production opponent AI. They use an independent eight-line win oracle.

## Run

From the repository root, using .NET 9:

```powershell
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_NOLOGO = '1'
dotnet test tests/TicTacToe.WebMultiplayer.Tests -c Release --filter FullyQualifiedName~GameplayPlaytesterTests --logger 'trx;LogFileName=gameplay-playtesters.trx' --results-directory artifacts/test-results/playtesters
```

The tests start their own server on a random loopback port. They do not use the
live website or an existing development server. No accounts or paid APIs are used.

`DOTNET_GENERATE_ASPNET_CERTIFICATE=false` is Microsoft's documented switch to
prevent SDK first-run development-certificate generation. The root
`test-multiplayer.ps1` runner sets it only for its invocation and restores the
previous process value afterward. `DOTNET_NOLOGO` suppresses the welcome banner;
it does not control certificates. `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` has been
unsupported since .NET Core 3.0 and is not a certificate safeguard. See
[Microsoft's environment-variable reference](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables#dotnet_generate_aspnet_certificate).

Use a current .NET 9 servicing runtime for verification. A clean NuGet audit does
not audit the installed shared ASP.NET Core runtime. The October 2026 verification
also runs the built test assembly on isolated Microsoft.NETCore.App and
Microsoft.AspNetCore.App 9.0.21 runtimes, downloaded from Microsoft's release
manifest and verified against its SHA-512 hashes. Installing that task-local copy
does not update any machine-wide runtime or deployment.

## Finite matrix

Seeds are **13, 2026, 104729**. Each test case retains its seed and ordered action
trace; failures include the board before each move. Rerun the same case/seed to
reproduce it. Random choices are made by the .NET runtime, so compare traces
within this implementation rather than assuming the Java port has identical RNG.

| Profile / path | Total rounds | Checks |
| --- | ---: | --- |
| Random and tactical hot-seat players, `LocalGameSession` | 24 | Legal turn counts, one-cell mutation, out-of-range/duplicate/terminal rejection, valid winner/draw, scores once per round, replay, reset, menu interruption, session isolation |
| Human playtesters versus production Warlord, both seats | 6 | Random seed 13 and tactical seeds 2026/104729; Warlord never loses; no occupied-cell overwrite, bounded completion, correct score and cleared thinking flag |
| Random and tactical peers over real SignalR | 6 | Challenge/accept, legal game to result, invalid/wrong-turn/outsider commands, duplicate moves, reconnect after two plies, board preservation, old connection revocation, spectator isolation, match cleanup |

The tactical player wins immediately when possible, blocks a one-move threat,
takes the center, then chooses a seeded legal move. It does not call production
AI to decide its actions. This makes it useful for checking that AI separately.

Each round is bounded by nine legal moves. Each wire message wait is capped at
five seconds. Per-seed budgets are ten seconds for the local matrix, thirty
seconds for two AI rounds (including the intentional 600 ms deliberation), and
sixty seconds for two network rounds. These are regression budgets, not load-test
or end-user latency guarantees. Normal runs finish well below them.

## Evidence and boundaries

The TRX file contains named seeds, elapsed time and coverage summaries; failures
include the action sequence and last observed board. Keep that file with the
revision used for reproduction. Existing focused `MatchRegistrySecurityTests`,
`GameHubIntegrationTests`, and `LocalGameSessionTests` cover controlled timer
expiry, simultaneous disconnects and stale delayed-AI cancellation in greater
detail; these playtesters complement those tests.

The separate `tools/playtest/browser-smoke.mjs` runner uses tool-bound browser
tabs for rendered keyboard/control paths and screenshots. The suite here is
**headless service/transport gameplay**, not rendered browser acceptance. Neither
kind of bot establishes subjective fun, polish or novice comprehension. This is
a finite sample, not exhaustive proof, a production soak or a concurrency/load
benchmark. The in-memory server intentionally cannot resume a match across a
server process restart.

## Rendered browser runner

The reusable browser runner takes an existing, authorized `cua_repl` tab; it does
not launch a browser or attach to the user's accounts. Start a local test server,
open its menu in a dedicated tool-controlled tab, then call:

```javascript
const runner = await import("file:///ABSOLUTE/REPOSITORY/tools/playtest/browser-smoke.mjs");
const result = await runner.runLocalSmoke(tab, { seed: 20261025 });
```

It verifies a keyboard-entered win, nine labeled cell buttons, disabled occupied
cells, a cleared replay board, and two seeded random rounds with legal turn counts.
Its ordered DOM-backed trace and elapsed times are returned for saving alongside
the tested revision. The 90-second cap is a regression budget. An optional
`capture(name, jpegBytes)` callback receives screenshots at checkpoints and on
failure. A thrown error carries the failure trace as `error.playtest`.

The October 2026 verification used this runner through the supported browser tool,
plus separate two-tab challenge, reconnect and forfeit checks.
