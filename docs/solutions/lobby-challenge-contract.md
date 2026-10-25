# Lobby entries and challenge acknowledgements need shared identities

## Symptoms and root causes

The first browser in the lobby displayed a later arrival but its Challenge button
sent a null target. Snapshots used `connectionId`, while `OnPlayerJoined` used
`connId`; the component deserialized both into a `ConnectionId` property. Protocol
tests that challenged a client's separately known connection ID missed this UI path.

Cancellation also sent the literal placeholder `(awaiting server)` because the
server never acknowledged the challenge ID to its sender. The recipient kept an
active invitation. Separately, responding consumed an invitation before validating
its recipient, and acceptance did not check the deadline between sweeper ticks.

## Resolution

Snapshots and live events now use the shared `LobbyPlayer` contract. Challenge
creation returns its ID and first sends `OnChallengeSent` to its sender, before
delivering the invitation to its recipient. This ordering lets even a fast decline
or cancellation be matched to the correct pending request. Cancellation, departure,
decline and expiry update only the corresponding invitation. The broker validates
recipient and deadline before consuming an invitation and allows one pending
invitation per player. Invalid, unavailable and self-targets are rejected.

## Verification and prevention

Two integration tests failed before the fix: deserializing the live join payload
and receiving a real challenge acknowledgement. `GameHubIntegrationTests` also
covers cancellation on both clients, late acceptance, outsider accept/decline,
invalid targets and expiry before a sweep. Use IDs deserialized from actual server
messages in future workflow tests, rather than bypassing the UI contract with
connection IDs obtained out of band. Keep challenge event handlers scoped by ID.
