# Tic-Tac-Toe Network Multiplayer

A Blazor Server app that adds three play modes to the Order vs Rebellion theme:
hot-seat, vs CPU (three difficulty tiers), and network multiplayer with
auto-named lobby + challenge UX.

## Run locally

```bash
dotnet run --project web-multiplayer --urls http://localhost:5050
```

Open http://localhost:5050 in two browser windows to test multiplayer.

## Reconnect authorization

Each accepted player receives a separate 256-bit cryptographically random
`reconnectToken` in their private `OnChallengeAccepted` message. The client keeps
it in the current Blazor component's memory and supplies it with the match ID to
`RejoinMatch(matchId, reconnectToken)` after SignalR reconnects. This method
returns `true` on success and `false` if authorization or the reconnect window
is invalid. Knowing a match ID alone does not authorize reconnection.

The token identifies one player in one match. A different connection can take
that player's seat only while it is disconnected and before its 30-second grace
deadline. The token stays valid across repeated disconnects during the match;
it is invalidated on a win, draw, forfeit, or match removal. Tokens are excluded
from shared state and lobby messages. Reconnection, disconnect expiry, moves,
and forfeits use the registry lock so stale timer callbacks cannot forfeit a
successfully rejoined player. A forfeit requires the caller's current connection
to belong to that match.

This preserves automatic SignalR reconnection within the existing Blazor
circuit. Reloading the page, closing its circuit, or restarting the server does
not preserve the match credentials. Tokens are bearer credentials: serve the
app over HTTPS and do not put them in URLs or logs.

## Automated tests

With the .NET 9 SDK installed, run:

```bash
dotnet test tests/TicTacToe.WebMultiplayer.Tests -c Release
```

The suite covers token validation and lifecycle, participant authorization,
disconnect and expiry races, and real SignalR clients reconnecting and
continuing a match through a local test server.

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

If the reverse proxy is on the **same host** as the app, `--urls
http://127.0.0.1:5000` is fine (and recommended — keeps the app off the
network). If the reverse proxy is on a **different host** (e.g. another
container reaching the app over a LAN bridge), bind to `0.0.0.0:5000` or the
host's LAN IP so the proxy can connect.
