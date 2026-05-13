using Blazored.Toast;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Promethaion.Web;
using Promethaion.Web.Components;
using Promethaion.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// API Base URL
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7161";

builder.Services.AddScoped(sp =>
    new HttpClient
    {
        BaseAddress = new Uri(apiBaseUrl)
    });

builder.Services.AddScoped<IPromethaionApiClient, PromethaionApiClient>();
builder.Services.AddScoped<TrainingHubService>();

builder.Services.AddBlazoredToast();

await builder.Build().RunAsync();