using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AniScroll.Web;
using AniScroll.Shared.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<AniListAuthService>();

builder.Services.AddScoped(sp => new HttpClient
{
    Timeout = TimeSpan.FromSeconds(30)
});

builder.Services.AddScoped<AniListService>();

// Singleton so the list persists for the whole session
builder.Services.AddSingleton<UserListService>();

await builder.Build().RunAsync();