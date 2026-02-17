using Microsoft.AspNetCore.Components.WebView;

namespace AniScroll.Maui
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

            // Intercept all URL navigation in the BlazorWebView.
            // External URLs (anything that isn't our local app) get opened
            // in the system browser instead of inside the WebView.
            // This prevents third-party JS (Crunchyroll, YouTube, etc.)
            // from executing in the sandboxed WebView environment and crashing.
            blazorWebView.UrlLoading += OnUrlLoading;
        }

        private void OnUrlLoading(object? sender, UrlLoadingEventArgs e)
        {
            var uri = e.Url;

            // Allow internal Blazor app navigation (localhost / app scheme)
            if (uri.Host == "0.0.0.0" ||
                uri.Host == "localhost" ||
                uri.Scheme == "app" ||
                uri.Scheme == "about" ||
                uri.Scheme == "data")
            {
                e.UrlLoadingStrategy = UrlLoadingStrategy.OpenInWebView;
                return;
            }

            // All external URLs → open in the system browser (Safari, Chrome, etc.)
            // and cancel WebView navigation entirely
            e.UrlLoadingStrategy = UrlLoadingStrategy.OpenExternally;
        }
    }
}