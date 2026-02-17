using Microsoft.AspNetCore.Components.WebView;

namespace AniScroll.Maui
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

            blazorWebView.UrlLoading += OnUrlLoading;
        }

        private void OnUrlLoading(object? sender, UrlLoadingEventArgs e)
        {
            var uri = e.Url;

            // Sur Windows, MAUI BlazorWebView utilise 0.0.0.1 (pas 0.0.0.0) comme hôte interne.
            // Sans ce filtre, toutes les pages internes sont ouvertes dans le navigateur externe
            // → fenêtre noire + onglet navigateur qui s'ouvre.
            if (uri.Host == "0.0.0.0" ||
                uri.Host == "0.0.0.1" ||       // ← CORRECTION : hôte réel sur Windows
                uri.Host == "localhost" ||
                uri.Scheme == "app" ||
                uri.Scheme == "about" ||
                uri.Scheme == "data")
            {
                e.UrlLoadingStrategy = UrlLoadingStrategy.OpenInWebView;
                return;
            }

            // Tous les liens externes → navigateur système
            e.UrlLoadingStrategy = UrlLoadingStrategy.OpenExternally;
        }
    }
}