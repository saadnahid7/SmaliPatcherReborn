using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmaliPatcherReborn.Core;

public sealed record Wallet(string Coin, string Ticker, string Network, string Address);

/// <summary>The wallets come from donate/wallets.json, embedded at build time. With any address missing nothing donation-related is shown.</summary>
public static class Donate
{
    public static readonly IReadOnlyList<Wallet> Wallets = Load();
    public static bool Enabled => Wallets.Count > 0;

    static List<Wallet> Load()
    {
        try
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("wallets.json");
            if (s == null) return new();
            using var doc = JsonDocument.Parse(s);
            var list = new List<Wallet>();
            foreach (var w in doc.RootElement.GetProperty("wallets").EnumerateArray())
            {
                var a = w.GetProperty("address").GetString()?.Trim() ?? "";
                if (!Regex.IsMatch(a, "^[A-Za-z0-9]{20,100}$")) return new();   // blank or odd address: show none rather than a wrong one
                list.Add(new Wallet(w.GetProperty("coin").GetString()!, w.GetProperty("ticker").GetString()!, w.GetProperty("network").GetString()!, a));
            }
            return list;
        }
        catch { return new(); }
    }
}
