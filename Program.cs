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