using Mahgoub_Store_Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<CompanyDatabaseConnectionProvider>();
builder.Services.AddSingleton<StoreTenantService>();
builder.Services.AddSingleton<PosSignedClient>();
builder.Services.AddHttpClient("PosTunnel", c =>
{
    c.Timeout = TimeSpan.FromSeconds(12);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = false
});

string[] extraOrigins = builder.Configuration.GetSection("StoreGateway:AllowedOriginHosts").Get<string[]>()
    ?? ["mahgoubonline.com", "localhost"];

builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
    {
        p.SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var u))
                    return false;

                string host = u.Host.ToLowerInvariant();
                foreach (string allowed in extraOrigins)
                {
                    string a = allowed.Trim().ToLowerInvariant();
                    if (host == a || host.EndsWith("." + a, StringComparison.Ordinal))
                        return true;
                }

                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();
app.UseCors();
app.MapGet("/healthz", () => Results.Text("ok", "text/plain"));
app.MapControllers();
app.Run();
