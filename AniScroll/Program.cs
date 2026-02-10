using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AniScroll;
using AniScroll.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configuration du HttpClient pour Blazor WASM avec l'API AniList
builder.Services.AddScoped(sp => 
{
    var httpClient = new HttpClient
    { 
        Timeout = TimeSpan.FromSeconds(30)
    };
    
    // Headers essentiels pour l'API AniList
    httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    
    return httpClient;
});

// Configuration du service AniList comme Scoped (une instance par utilisateur/session de navigateur)
// Dans Blazor WASM, Scoped = une instance par circuit utilisateur, donc isolé par utilisateur
builder.Services.AddScoped<AniListService>();

await builder.Build().RunAsync();