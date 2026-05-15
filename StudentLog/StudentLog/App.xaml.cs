using Microsoft.Extensions.Logging;
using StudentLog.Infrastructure.Data;

namespace StudentLog
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
        private readonly AppShell _appShell;
        private readonly DatabaseInitializer _databaseInitializer;
        private readonly ILogger<App> _logger;
        private bool _databaseInitializationStarted;

        public App(AppShell appShell, DatabaseInitializer databaseInitializer, ILogger<App> logger)
        {
            InitializeComponent();
            _appShell = appShell;
            _databaseInitializer = databaseInitializer;
            _logger = logger;

            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(_appShell);

            if (!_databaseInitializationStarted)
            {
                _databaseInitializationStarted = true;
                _ = InitializeDatabaseAsync(window);
            }

            return window;
        }

        private async Task InitializeDatabaseAsync(Window window)
        {
            try
            {
                await _databaseInitializer.InitializeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database initialisation failed");

                if (window.Page is not null)
                {
                    await window.Page.DisplayAlertAsync(
                        "Database Error",
                        "Could not connect to MySQL. The app opened, but data features may not work until the database is reachable.",
                        "OK");
                }
            }
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            _logger.LogError(e.Exception, "[APP] Unobserved task exception");

            if (e.Exception?.InnerException is OperationCanceledException or TaskCanceledException)
            {
                _logger.LogInformation("[APP] Suppressing expected cancellation exception");
                e.SetObserved();
            }
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                _logger.LogError(ex, "[APP] Unhandled exception");
            }
        }
    }
}
