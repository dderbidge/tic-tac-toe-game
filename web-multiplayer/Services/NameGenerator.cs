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
