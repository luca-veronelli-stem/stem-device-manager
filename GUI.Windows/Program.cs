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
///@version  0.4.3
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
/// 0.4.0: + File logger sink + decoder/telemetry silent-drop warnings.
///        + Restored Boot Interface tab for non-SPARK firmware uploads (#95).
///        + Fix: API DataType normalization + signed Int8/16/32 widths (#96),
///          telemetry stop after BLE reconnect (#104), unknown reply commands
///          tolerated by PacketDecoder (#100), command bytes from selected
///          Command not combobox index (#107).
///        + Security: rotated Azure dictionary API key; key relocated to
///          DictionaryApi__ApiKey env var (#94 stopgap).
/// 0.4.1: + Fix: appsettings.Production.json overlay now actually loads at
///          runtime, matching the v0.4.0 docs (#110, refs #94).
///        + SHIPPED_README.txt rewritten for v0.4.1 — API key is required,
///          no longer claims an embedded test key is present.
/// 0.4.2: + CI fix: release workflow now also uploads appsettings.json +
///          README.txt (technician config + procedure) alongside the exe.
///          v0.4.1 release shipped exe-only, leaving DictionaryApi:BaseUrl
///          null at runtime which silently forced Excel-only mode.
/// 0.4.3: + Relocate logs to %LocalAppData%\Stem\DeviceManager\logs\ per
///          the STEM APP_DATA standard v1.9.0 (no more polluting the
///          install dir; survives read-only Program Files installs).
///        + Log resolved DictionaryApi:ApiKey source at startup (#114) so
///          the silent-Excel-fallback chain is one log line away.
///        + CI: release artifacts now ship as a single zip
///          (stem-device-manager-<tag>.zip) so a tech can't accidentally
///          download an incomplete set.
///
/// TODO:
/// - Completare decodifica faults
///
/// @attention
///
/// <h2><center>&copy; COPYRIGHT 2025 STEM </center></h2>
///
/// </summary>
using GUI.Windows.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StemPC;
using STEMPM;

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
                .AddJsonFile("appsettings.Production.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            // Source-of-truth for the Azure dictionary API key (#114). Captured
            // here so a bench post-mortem can tell at a glance which auth route
            // the app picked without inspecting env / files by hand. The value
            // itself is never read or logged -- only the source label.
            var apiKeySource = Diagnostics.ApiKeySourceDetector.Detect(configuration);

            // Configurazione della dependency injection
            var services = new ServiceCollection();

            // Logging: Debug sink (visible in Output window when running under VS) +
            // per-process file sink under %LocalAppData%\Stem\DeviceManager\logs\ so
            // bench sessions and post-mortems can tail/inspect what happened without a
            // debugger attached. Location resolves via StemAppData per APP_DATA v1.9.0.
            var logPath = Path.Combine(
                Diagnostics.StemAppData.GetLogsDir(),
                $"app-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            var fileLoggerProvider = new FileLoggerProvider(logPath);
            services.AddLogging(b => b
                .AddDebug()
                .AddProvider(fileLoggerProvider)
                .SetMinimumLevel(LogLevel.Debug));

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

            // #114: log the resolved DictionaryApi:ApiKey source (Empty /
            // AppSettings / ProductionFile / Env / Unknown). One Information
            // line on the GUI.Windows.Program category, no key value, just the
            // label. This is the first log line a bench post-mortem reads
            // when chasing a silent-Excel-fallback.
            serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("GUI.Windows.Program")
                .LogInformation("Dictionary API key source: {Source}", apiKeySource);

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
                // PCANManager is IAsyncDisposable-only (PR #117), so the sync
                // ServiceProvider.Dispose() throws InvalidOperationException.
                // Blocking here is safe: the message loop has already exited.
                serviceProvider.DisposeAsync().AsTask().GetAwaiter().GetResult();
#endif
                // AddProvider(instance) does not transfer ownership to the DI container,
                // so the file logger must be disposed explicitly to flush + close.
                fileLoggerProvider.Dispose();
            }
        }
    }
}