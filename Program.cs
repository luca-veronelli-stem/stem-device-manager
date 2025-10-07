/// <summary>
///*****************************************************************************
/// @file    Program.cs
/// @author  Michele Pignedoli
///@version  2.14
/// @date    27/10/2025
/// @brief   STEM Device Manager Main program body
///*****************************************************************************
///
/// 2.10: + TopLift A2 and Eden XP activation
/// 2.11: + Read firmware version activation for mainboard
/// 2.14: + CAN baud rate variable
///       + uart suport
///
///
/// TODO:
/// - Completare decodifica faults
///
/// @attention
///
/// <h2><center>&copy; COPYRIGHT 2025 STEM </center></h2>
///
/// </summary>

using STEMPM;

namespace StemPC
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // Mostra la schermata di avvio
            SplashScreen splash = new SplashScreen();
            splash.Show();
            Application.DoEvents();

            // Crea il main form
            Form1 mainForm = new Form1();
            mainForm.Load += (sender, e) => splash.Close(); // Chiude la splash screen all'avvio del MainForm

            Application.Run(mainForm);
        }
    }
}