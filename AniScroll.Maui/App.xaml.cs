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

            // Configuration de la taille de fenêtre (format mobile)
            const int mobileWidth = 400;  // Largeur mobile
            const int mobileHeight = 800; // Hauteur mobile

            window.Created += (s, e) =>
            {
                // Définir la taille initiale de la fenêtre
                window.Width = mobileWidth;
                window.Height = mobileHeight;

#if WINDOWS
                // Configuration spécifique Windows
                var displayInfo = DeviceDisplay.Current.MainDisplayInfo;
                
                // Centrer la fenêtre sur l'écran
                window.X = (displayInfo.Width / displayInfo.Density - mobileWidth) / 2;
                window.Y = (displayInfo.Height / displayInfo.Density - mobileHeight) / 2;
                
                // Définir les tailles min/max pour permettre le redimensionnement
                window.MinimumWidth = 300;
                window.MinimumHeight = 600;
                window.MaximumWidth = 1200;
                window.MaximumHeight = 1600;
#endif
            };

            return window;
        }
    }
}
