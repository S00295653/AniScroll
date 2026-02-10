using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AniScroll;
using AniScroll.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient simple - les headers sont gérés dans AniListService
builder.Services.AddScoped(sp => new HttpClient 
{ 
    Timeout = TimeSpan.FromSeconds(30)
});

// Service AniList avec le nouveau système de buffer progressif
builder.Services.AddScoped<AniListService>();

await builder.Build().RunAsync();