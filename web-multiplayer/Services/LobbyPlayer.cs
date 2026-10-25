namespace TicTacToe.WebMultiplayer.Services;

// The snapshot and live-join event use one contract so both create usable entries.
public sealed record LobbyPlayer(string ConnectionId, string Name);
