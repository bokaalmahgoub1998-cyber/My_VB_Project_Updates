using System.Text;

namespace Mahgoub_Store.Helpers;

public static class StoreSafeText
{
    public static string PersonName(string? raw) => Clean(raw, 80, false);
    public static string Note(string? raw) => Clean(raw, 300, true);
    public static string Address(string? raw) => Clean(raw, 300, true);
    public static string ItemNote(string? raw) => Clean(raw, 200, false);

    public static string Clean(string? raw, int maxLen, bool allowNewLine)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";
        var sb = new StringBuilder(Math.Min(raw.Length, maxLen));
        foreach (var ch in raw.Trim())
        {
            if (ch is '<' or '>' or '`' or '\0')
                continue;
            if (char.IsControl(ch) && !(allowNewLine && ch is '\n' or '\r'))
                continue;
            sb.Append(ch);
            if (sb.Length >= maxLen)
                break;
        }
        string s = sb.ToString().Trim();
        while (s.Contains("--", StringComparison.Ordinal))
            s = s.Replace("--", "-", StringComparison.Ordinal);
        s = s.Replace("/*", "").Replace("*/", "");
        return s.Length > maxLen ? s[..maxLen] : s;
    }
}
