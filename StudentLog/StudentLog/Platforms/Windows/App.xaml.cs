using Microsoft.UI.Xaml;
using StudentLog.Platforms.Windows;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace StudentLog.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            base.OnLaunched(args);
            WindowsSoundSuppressor.Suppress();

            // Restore sounds on any exit path: normal window close, task-kill, etc.
            AppDomain.CurrentDomain.ProcessExit += (_, _) => WindowsSoundSuppressor.Restore();
        }
    }

}
