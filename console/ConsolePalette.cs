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
        _                => ThemeText.SymbolOf(p)
    };

    public static string ColoredNameOf(Player p) => p switch
    {
        Player.Ascendant => Color(AnsiGold, ThemeText.AscendantName),
        Player.Vanguard  => Color(AnsiCrimson, ThemeText.VanguardName),
        _                => ThemeText.NameOf(p)
    };

    public static string PlacementMessage(Player p, string sector) =>
        ThemeText.PlacementMessageFor(ColoredNameOf(p), sector);

    public static string WinMessage(Player p) =>
        ThemeText.WinMessageFor(ColoredNameOf(p));
}
