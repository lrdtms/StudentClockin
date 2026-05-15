using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StudentLog.Application.Interfaces;
using StudentLog.Application.Services;
using StudentLog.Core.Interfaces;
using StudentLog.Core.Interfaces.Repositories;
using StudentLog.Infrastructure.Data;
using StudentLog.Infrastructure.Repositories;
using StudentLog.Infrastructure.Services;
using StudentLog.UI.Services;
using StudentLog.UI.ViewModels;
using StudentLog.UI.Views;
#if WINDOWS
using StudentLog.Platforms.Windows;
#endif

namespace StudentLog
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>();

            builder.Logging.AddDebug();

            // Load appsettings.json from the app output directory
            var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(appSettingsPath, optional: true, reloadOnChange: false)
                .Build();

            builder.Configuration.AddConfiguration(configuration);
            builder.Services.Configure<MySqlOptions>(builder.Configuration.GetSection("MySql"));

            builder.Services.AddSingleton<IDbConnectionFactory, MySqlConnectionFactory>();
            builder.Services.AddSingleton<DatabaseInitializer>();

            builder.Services.AddSingleton<ICohortRepository, CohortRepository>();
            builder.Services.AddSingleton<IStudentRepository, StudentRepository>();

            builder.Services.AddSingleton<ICohortService, CohortService>();
            builder.Services.AddSingleton<IStudentService, StudentService>();
            builder.Services.AddSingleton<IAttendanceService, AttendanceService>();
            builder.Services.AddSingleton<ISessionStateService, SessionStateService>();
            builder.Services.AddSingleton<INfcService, NfcService>();
            builder.Services.AddSingleton<IDialogService, MauiDialogService>();

#if WINDOWS
            builder.Services.AddTransient<ICsvExportService, CsvExportService>();
#endif

            builder.Services.AddSingleton<AppShell>();

            builder.Services.AddTransient<CheckInSessionViewModel>();
            builder.Services.AddTransient<CohortsViewModel>();
            builder.Services.AddTransient<StudentsViewModel>();
            builder.Services.AddTransient<StudentHistoryViewModel>();

            builder.Services.AddTransient<CheckInSessionPage>();
            builder.Services.AddTransient<CohortsPage>();
            builder.Services.AddTransient<StudentsPage>();
            builder.Services.AddTransient<StudentHistoryPage>();

            return builder.Build();
        }
    }
}
