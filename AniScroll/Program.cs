using AniScroll.Services;
using AniScroll;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient configuré pour WASM
builder.Services.AddScoped(sp => new HttpClient());

// Service AniList
builder.Services.AddScoped<AniListService>();

await builder.Build().RunAsync();