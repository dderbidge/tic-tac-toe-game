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

    // Templates that take a pre-rendered name (for both plain and colored callers).
    public static string PlacementMessageFor(string renderedName, string sector) =>
        $"{renderedName} claims sector {sector}.";

    public static string WinMessageFor(string renderedName) =>
        $"{renderedName} holds the line — three sectors in a row.";

    // Player-keyed convenience overloads using the plain name.
    public static string PlacementMessage(Player p, string sector) =>
        PlacementMessageFor(NameOf(p), sector);

    public static string WinMessage(Player p) =>
        WinMessageFor(NameOf(p));

    public static string DrawMessage() =>
        "Stalemate. The galaxy holds its breath.";

    public static string OccupiedMessage(string sector) =>
        $"Sector {sector} is already claimed.";

    public static string InvalidCoordMessage() =>
        "Invalid coordinates, captain. Use A1 through C3.";
}
