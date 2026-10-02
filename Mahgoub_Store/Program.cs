using Mahgoub_Store;
using Mahgoub_Store.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<StoreBrandHub>();
builder.Services.AddScoped<TenantHostService>();
builder.Services.AddScoped<CentralRegistryService>();
builder.Services.AddScoped<StoreApiClient>();
builder.Services.AddScoped<DeviceFingerprintService>();
builder.Services.AddSingleton<CartService>();
builder.Services.AddScoped<OrderLockService>();
builder.Services.AddSingleton<MenuCacheService>();
builder.Services.AddSingleton<StoreRunGate>();
builder.Services.AddScoped(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(20) });

await builder.Build().RunAsync();
