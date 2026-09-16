using System.Security.Cryptography;
using System.Text;

namespace Mahgoub_Store_Api.Security;

/// <summary>
/// توقيع طلبات الجسر إلى جهاز المحل. المفتاح هو d_name (مفتاح المنتج) — لا يُرسل في الطلب.
/// </summary>
public static class WebStoreGatewayHmac
{
    public const string TimestampHeader = "X-Mahgoub-Ts";
    public const string NonceHeader = "X-Mahgoub-Nonce";
    public const string SignatureHeader = "X-Mahgoub-Sign";
    public static readonly TimeSpan MaxSkew = TimeSpan.FromMinutes(5);

    public static string Compute(string productKey, string method, string pathAndQuery, string unixSeconds, string nonce, byte[]? body)
    {
        string bodyHash = Convert.ToHexString(SHA256.HashData(body ?? [])).ToLowerInvariant();
        string msg = $"{unixSeconds}\n{nonce}\n{method.Trim().ToUpperInvariant()}\n{pathAndQuery}\n{bodyHash}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(productKey.Trim()));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(msg))).ToLowerInvariant();
    }

    public static bool SignaturesEqual(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes((a ?? "").Trim().ToLowerInvariant()),
            Encoding.UTF8.GetBytes((b ?? "").Trim().ToLowerInvariant()));
}
