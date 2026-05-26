using System;
using System.IO;
using System.Text;
using TicTacToe.Core;

namespace TicTacToe;

public static class Renderer
{
    public static void Initialize()
    {
        Console.OutputEncoding = Encoding.UTF8;

        bool disable =
            Console.IsOutputRedirected ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")) ||
            string.Equals(Environment.GetEnvironmentVariable("TERM"), "dumb", StringComparison.OrdinalIgnoreCase);

        ConsolePalette.UseColor = !disable;
    }

    public static void DrawTitle()
    {
        Console.WriteLine();
        Console.WriteLine(ConsolePalette.Color(ConsolePalette.AnsiBold,
            "═══ ORDER VS REBELLION — A SECTOR DUEL ═══"));
        Console.WriteLine(
            $"  {ConsolePalette.ColoredNameOf(Player.Ascendant)} ({ConsolePalette.ColoredSymbolOf(Player.Ascendant)})  " +
            $"vs  {ConsolePalette.ColoredNameOf(Player.Vanguard)} ({ConsolePalette.ColoredSymbolOf(Player.Vanguard)})");
        Console.WriteLine();
    }

    public static void DrawBoard(Board board)
    {
        string Cell(int r, int c) => ConsolePalette.ColoredSymbolOf(board.At(r, c));

        Console.WriteLine();
        Console.WriteLine("       A   B   C");
        Console.WriteLine("     ┌───┬───┬───┐");
        Console.WriteLine($"   1 │ {Cell(0, 0)} │ {Cell(0, 1)} │ {Cell(0, 2)} │");
        Console.WriteLine("     ├───┼───┼───┤");
        Console.WriteLine($"   2 │ {Cell(1, 0)} │ {Cell(1, 1)} │ {Cell(1, 2)} │");
        Console.WriteLine("     ├───┼───┼───┤");
        Console.WriteLine($"   3 │ {Cell(2, 0)} │ {Cell(2, 1)} │ {Cell(2, 2)} │");
        Console.WriteLine("     └───┴───┴───┘");
        Console.WriteLine();
    }

    public static void DrawScore(int ascendantWins, int vanguardWins, int draws)
    {
        var a = $"{ConsolePalette.ColoredNameOf(Player.Ascendant)} {ConsolePalette.ColoredSymbolOf(Player.Ascendant)} {ascendantWins}";
        var v = $"{ConsolePalette.ColoredNameOf(Player.Vanguard)} {ConsolePalette.ColoredSymbolOf(Player.Vanguard)} {vanguardWins}";
        var d = $"Stalemates {draws}";
        Console.WriteLine($"  {a}   —   {v}   —   {d}");
    }

    public static void Announce(string message)
    {
        Console.WriteLine();
        Console.WriteLine($"  {message}");
        Console.WriteLine();
    }

    public static void Error(string message)
    {
        Console.WriteLine($"  {ConsolePalette.Color(ConsolePalette.AnsiCrimson, "!")}  {message}");
    }

    public static string ReadLine(string prompt)
    {
        Console.Write(prompt);
        var line = Console.ReadLine();
        if (line is null)
            throw new EndOfStreamException("Input stream closed. Quitting.");
        return line;
    }

    public static (int row, int col)? ParseCoordinate(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        char letter = '\0';
        char digit  = '\0';

        foreach (char ch in input)
        {
            if (char.IsWhiteSpace(ch)) continue;
            if (char.IsLetter(ch))
            {
                if (letter != '\0') return null;
                letter = char.ToUpperInvariant(ch);
            }
            else if (char.IsDigit(ch))
            {
                if (digit != '\0') return null;
                digit = ch;
            }
            else
            {
                return null;
            }
        }

        if (letter < 'A' || letter > 'C') return null;
        if (digit  < '1' || digit  > '3') return null;

        int col = letter - 'A';
        int row = digit  - '1';
        return (row, col);
    }

    public static string SectorLabel(int row, int col)
        => $"{(char)('A' + col)}{row + 1}";

    public static (int row, int col) PromptForCoordinate(Player active, Board board)
    {
        while (true)
        {
            string line = ReadLine($"  {ConsolePalette.ColoredNameOf(active)} — claim a sector (A1–C3): ");
            var parsed = ParseCoordinate(line);
            if (parsed is null)
            {
                Error(ThemeText.InvalidCoordMessage());
                continue;
            }
            var (r, c) = parsed.Value;
            if (board.At(r, c) != Player.None)
            {
                Error(ThemeText.OccupiedMessage(SectorLabel(r, c)));
                continue;
            }
            return (r, c);
        }
    }

    public static int PromptMenu(string title, string[] options)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine($"  {ConsolePalette.Color(ConsolePalette.AnsiBold, title)}");
            for (int i = 0; i < options.Length; i++)
                Console.WriteLine($"    {i + 1}. {options[i]}");

            string line = ReadLine("  Select: ").Trim();
            if (int.TryParse(line, out int n) && n >= 1 && n <= options.Length)
                return n - 1;

            Error("Pick a number from the menu.");
        }
    }

    public static bool PromptYesNo(string question)
    {
        while (true)
        {
            string line = ReadLine($"  {question} (y/n): ").Trim().ToLowerInvariant();
            if (line is "y" or "yes") return true;
            if (line is "n" or "no")  return false;
            Error("Answer y or n.");
        }
    }
}
