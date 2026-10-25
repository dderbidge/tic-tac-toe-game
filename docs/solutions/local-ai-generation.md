# Delayed AI turns must belong to the current match

## Symptom and cause

Both `web/Services/GameSession.cs` and `web-multiplayer/Services/LocalGameSession.cs`
wait 600 ms before an AI move. The original continuation used the session's current
board and faction without checking whether the player had left or replaced that
match. A pending turn could mark a new hot-seat board or advance the wrong faction
after duplicate AI starts. Checking `InMatch` alone cannot distinguish two matches.

## Resolution

Each session increments its match generation when starting a match or leaving for
the menu/setup, and immediately clears its thinking state. AI continuations retain
the generation they started with and return without mutation when it is obsolete.
The current match's thinking flag is never cleared by an older continuation.

## Verification and prevention

`LocalGameSessionTests` runs against both session implementations. Six regression
cases failed before the guard and passed afterward: leaving during AI deliberation,
replacing the match with hot-seat, and starting AI twice. Adjacent cases cover invalid
and duplicate moves, no moves after a win, replay, and score reset. The existing 27
multiplayer reconnect/authorization tests remain the baseline.

Keep the same generation check when adding any delayed local-game action. These
sessions are owned by the Blazor dispatcher; this guard is for asynchronous stale
work, not a replacement for synchronization in shared multiplayer registries.
