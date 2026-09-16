using Mahgoub_Store_Api.Security;

namespace Mahgoub_Store_Api.Services;

public sealed class PosSignedClient
{
    private readonly IHttpClientFactory _http;

    public PosSignedClient(IHttpClientFactory http) => _http = http;

    public async Task<HttpResponseMessage> SendAsync(
        string tunnelBase,
        string productKey,
        HttpMethod method,
        string pathAndQuery,
        byte[]? body,
        string? accept,
        CancellationToken ct)
    {
        string baseUrl = tunnelBase.Trim().TrimEnd('/');
        string path = pathAndQuery.StartsWith('/') ? pathAndQuery : "/" + pathAndQuery;
        string ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        string nonce = Guid.NewGuid().ToString("N");
        string sign = WebStoreGatewayHmac.Compute(productKey, method.Method, path, ts, nonce, body);

        var req = new HttpRequestMessage(method, baseUrl + path);
        req.Headers.TryAddWithoutValidation(WebStoreGatewayHmac.TimestampHeader, ts);
        req.Headers.TryAddWithoutValidation(WebStoreGatewayHmac.NonceHeader, nonce);
        req.Headers.TryAddWithoutValidation(WebStoreGatewayHmac.SignatureHeader, sign);
        if (!string.IsNullOrWhiteSpace(accept))
            req.Headers.TryAddWithoutValidation("Accept", accept);

        if (body is { Length: > 0 } || method != HttpMethod.Get)
            req.Content = new ByteArrayContent(body ?? []);

        if (req.Content is not null && (body is { Length: > 0 } || method == HttpMethod.Post))
            req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        var client = _http.CreateClient("PosTunnel");
        return await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
    }

    public async Task<bool> PingAsync(string tunnelBase, string productKey, CancellationToken ct)
    {
        try
        {
            using var resp = await SendAsync(tunnelBase, productKey, HttpMethod.Get, "/api/webstore/ping", null, null, ct)
                .ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
