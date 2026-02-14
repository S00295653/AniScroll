namespace AniScroll.Maui
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new MainPage())
            {
                Title = "AniScroll"
            };

            // Configuration de la taille de fenêtre optimisée
            const int mobileWidth = 400;
            const int mobileHeight = 850;

            window.Created += (s, e) =>
            {
                // Définir la taille initiale
                window.Width = mobileWidth;
                window.Height = mobileHeight;

#if WINDOWS
                var displayInfo = DeviceDisplay.Current.MainDisplayInfo;
                
                // Centrer la fenêtre
                window.X = (displayInfo.Width / displayInfo.Density - mobileWidth) / 2;
                window.Y = (displayInfo.Height / displayInfo.Density - mobileHeight) / 2;
                
                // Tailles min/max optimisées
                window.MinimumWidth = 375;
                window.MinimumHeight = 667;
                window.MaximumWidth = 480;
                window.MaximumHeight = 1024;
#endif
            };

            return window;
        }
    }
}