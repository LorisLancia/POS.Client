using System;
using System.Windows;
using POS.Client.Services;
using POS.Client.Views;

namespace POS.Client
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // IMPORTANTE: non chiudere l'app quando l'ultima finestra si chiude
            // Cosi possiamo chiudere il wizard e aprire MainWindow senza problemi
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                var configService = new ConfigService();
                var config = configService.LoadSetupConfig();

                if (config == null || !config.IsValid)
                {
                    // Primo avvio: apri wizard
                    var wizard = new SetupWizardWindow();
                    var ok = wizard.ShowDialog() ?? false;

                    if (!ok)
                    {
                        Shutdown();
                        return;
                    }

                    // Ricarica config dopo wizard
                    config = configService.LoadSetupConfig();
                    if (config == null || !config.IsValid)
                    {
                        MessageBox.Show("Setup configuration is invalid. The application will close.",
                            "Setup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        Shutdown();
                        return;
                    }
                }

                // Popola AppState dalla config salvata
                if (config != null)
                {
                    AppState.ServerUrl = config.ServerUrl;
                    AppState.MachineToken = config.MachineToken;
                    AppState.CurrentcompanyId = config.CompanyId;
                    AppState.HardwareId = config.HardwareId;
                    AppState.PosClientId = config.PosClientId;
                    AppState.WarehouseId = config.WarehouseId;
                    AppState.RegisterName = config.RegisterName;
                }

                // Avvia MainWindow
                var main = new MainWindow();
                main.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Application startup error: {ex.Message}\n\n{ex.StackTrace}",
                    "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}