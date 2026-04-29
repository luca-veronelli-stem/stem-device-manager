using Core.Interfaces;
using Infrastructure.Persistence;
using Infrastructure.Protocol;
using Infrastructure.Protocol.Hardware;
using Infrastructure.Protocol.Legacy;
using Microsoft.Extensions.Configuration;
using Services;
/// <summary>
///*****************************************************************************
/// @file    Program.cs
/// @author  Michele Pignedoli, Luca Veronelli
///@version  0.3.0
/// @date    20/10/2025
/// @brief   STEM Device Manager Main program body
///*****************************************************************************
///
/// 2.10:  + TopLift A2 and Eden XP activation
/// 2.11:  + Read firmware version activation for mainboard
/// 2.14:  + CAN baud rate variable
///        + uart support
/// 2.15:  + Slow telemetry fix
///        + fast telemetry activation
/// 0.3.0: + SemVer reset, multi-project architecture, Azure dictionary API,
///          ProtocolService/TelemetryService/BootService, ConnectionManager,
///          DictionaryCache, removal of #if device variants (runtime selection),
///          Spark BLE firmware stabilization (spec-001), Lean 4 invariants.
///
/// TODO:
/// - Completare decodifica faults
///
/// @attention
///
/// <h2><center>&copy; COPYRIGHT 2025 STEM </center></h2>
///
/// </summary>
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StemPC;
using STEMPM;
#if DEBUG
using GUI.Windows.Diagnostics;
#endif

namespace GUI.Windows
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Configurazione
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            // Configurazione della dependency injection
            var services = new ServiceCollection();

            // Logging: Debug sink (visible in Output window when running under VS)
            // is enough for now; a file/event sink can be added later if bring-up
            // sessions need persistent logs.
            services.AddLogging(b => b.AddDebug().SetMinimumLevel(LogLevel.Debug));

            // Provider dizionari (API Azure con fallback Excel, o solo Excel)
            services.AddDictionaryProvider(configuration);

            // Driver hardware legacy (Phase 4: spostati in Infrastructure.Protocol/Legacy/).
            // Verranno sostituiti quando Stem.Communication NuGet sarà disponibile.
            // Registriamo come tipo concreto + alias interfaccia perché UI tab (BLE_WF_Tab)
            // e Form1 hanno bisogno dell'istanza concreta (per API non esposte dall'interfaccia,
            // es. StartScanningAsync/ScanPorts). Lo stesso singleton viene risolto via IBleDriver
            // dal BlePort: non crea istanze multiple con stato desincronizzato.
            services.AddSingleton<BLEManager>();
            services.AddSingleton<IBleDriver>(sp => sp.GetRequiredService<BLEManager>());
            services.AddSingleton<SerialPortManager>();
            services.AddSingleton<ISerialDriver>(sp => sp.GetRequiredService<SerialPortManager>());

            // Adapter HW (CanPort/BlePort/SerialPort) + driver PCAN-USB
            services.AddProtocolInfrastructure();

            // Servizi pure-logic: IDeviceVariantConfig + IPacketDecoder vuoto
            // (UpdateDictionary chiamato dal consumer post-load Azure).
            // ProtocolService/TelemetryService/BootService NON registrati: dipendono
            // dalla port runtime, creati dal consumer (Phase 3 ConnectionManager).
            services.AddServices(configuration);

            // Provider di servizi per dependency injection
            var serviceProvider = services.BuildServiceProvider();

#if DEBUG
            // spec-001 T004 (research.md R8): Debug-only shutdown audit.
            // Logs every IDisposable owned by ConnectionManager, BootService,
            // BLEManager at dispose time with stack-trace. Disposing the
            // service provider on exit ensures singleton IDisposables run
            // through the audit; Release builds keep the previous behavior.
            ShutdownAudit.Enable(
                serviceProvider.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("GUI.Windows.Diagnostics.ShutdownAudit"));

            // spec-001 T007 (US1): a cycle-9 bench run exited with code
            // 0xffffffff and no dispose trail, suggesting an unhandled
            // exception on a non-UI thread (Plugin.BLE notification thread is
            // the likely source). Log crashes so the next occurrence leaves
            // evidence.
            var crashLogger = serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("GUI.Windows.Diagnostics.UnhandledException");
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                crashLogger.LogError(e.ExceptionObject as Exception,
                    "Unhandled AppDomain exception (IsTerminating={Terminating})",
                    e.IsTerminating);
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                crashLogger.LogError(e.Exception, "Unobserved Task exception");
                e.SetObserved();
            };
#endif

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // Mostra la schermata di avvio
            var variantConfig = serviceProvider.GetRequiredService<IDeviceVariantConfig>();
            SplashScreen splash = new(variantConfig);
            splash.Show();
            Application.DoEvents();

            // Crea il main form
            Form1 mainForm = new(serviceProvider);
            mainForm.Load += (sender, e) => splash.Close(); // Chiude la splash screen all'avvio del MainForm

            try
            {
                Application.Run(mainForm);
            }
            finally
            {
#if DEBUG
                serviceProvider.Dispose();
#endif
            }
        }
    }
}