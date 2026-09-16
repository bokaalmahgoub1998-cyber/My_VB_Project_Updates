using Mahgoub_Store.Models;

namespace Mahgoub_Store.Services;

public sealed class StoreBrandHub
{
    public event Action? Changed;
    public StoreBootstrapDto? Current { get; private set; }

    public void Publish(StoreBootstrapDto boot)
    {
        Current = boot;
        Changed?.Invoke();
    }
}

public static class StoreBrandCss
{
    public const string MahgoubNavy = "#254055";
    public const string Ink = "#111827";
    public const string Muted = "#6b7280";
    public const string OnFill = "#ffffff";

    public static string NormalizeHex(string? raw, string fallback = MahgoubNavy)
    {
        string t = (raw ?? "").Trim();
        if (t.Length == 7 && t[0] == '#')
        {
            foreach (char c in t[1..])
            {
                if (char.IsAsciiHexDigit(c))
                    continue;
                return fallback;
            }
            return t;
        }
        return fallback;
    }

    public static string ButtonFill(string hex)
    {
        var (r, g, b) = Rgb(NormalizeHex(hex));
        int guard = 0;
        while (Luminance(r, g, b) >= 0.54 && guard++ < 10)
        {
            r = Math.Max(0, (int)(r * 0.72));
            g = Math.Max(0, (int)(g * 0.72));
            b = Math.Max(0, (int)(b * 0.72));
        }
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    public static string ShellStyle(string color)
    {
        color = NormalizeHex(color);
        string fill = ButtonFill(color);
        return $"--brand-accent:{color};--brand-fill:{fill};--brand-primary:{fill};--on-brand:{OnFill};--text:{Ink};--navy:{MahgoubNavy};--muted:{Muted};";
    }

    private static (int r, int g, int b) Rgb(string hex) =>
        (Convert.ToInt32(hex[1..3], 16), Convert.ToInt32(hex[3..5], 16), Convert.ToInt32(hex[5..7], 16));

    private static double Luminance(int r, int g, int b) =>
        (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0;
}
