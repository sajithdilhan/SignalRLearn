using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SignalRLearn.Client;
using SignalRLearn.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7232";
var apiSettings = new ApiSettings(new Uri(apiBaseUrl, UriKind.Absolute));

builder.Services.AddSingleton(apiSettings);
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = apiSettings.BaseAddress });
builder.Services.AddScoped<OrderApiClient>();
builder.Services.AddScoped<OrderState>();
builder.Services.AddScoped<OrderRealtimeClient>();

await builder.Build().RunAsync();
