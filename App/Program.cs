using Core.Interfaces;
using Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
/// <summary>
///*****************************************************************************
/// @file    Program.cs
/// @author  Michele Pignedoli
///@version  2.15
/// @date    20/10/2025
/// @brief   STEM Device Manager Main program body
///*****************************************************************************
///
/// 2.10: + TopLift A2 and Eden XP activation
/// 2.11: + Read firmware version activation for mainboard
/// 2.14: + CAN baud rate variable
///       + uart suport
/// 2.15: + Fix telemetria lenta
///       + attivazione telemetria veloce
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
using StemPC;
using STEMPM;

namespace App
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

            // Provider dizionari (API Azure con fallback Excel, o solo Excel)
            services.AddDictionaryProvider(configuration);

            // Provider di servizi per dependency injection
            var serviceProvider = services.BuildServiceProvider();

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // Mostra la schermata di avvio
            SplashScreen splash = new();
            splash.Show();
            Application.DoEvents();

            // Crea il main form
            Form1 mainForm = new(serviceProvider);
            mainForm.Load += (sender, e) => splash.Close(); // Chiude la splash screen all'avvio del MainForm

            Application.Run(mainForm);
        }
    }
}