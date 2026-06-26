using System;
using System.Windows;
using POS.Client.Services;
using POS.Client.Views;
using POS.Client.Models; 

namespace POS.Client
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Mantieni l'app viva durante il wizard di setup e fino a quando la MainWindow è stata creata.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                var configService = new ConfigService();
                var config = configService.LoadSetupConfig();

                if (config == null || !config.IsValid)
                {
                    // Primo avvio: apri wizard
                    var wizard = new SetupWizardWindow();
                    bool? ok = wizard.ShowDialog();

                    if (ok != true)
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
                PopulateAppState(config);

                // Avvia MainWindow
                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                mainWindow.Show();
                ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Application startup error: {ex.Message}\n\n{ex.StackTrace}",
                    "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        public void PopulateAppState(SetupConfig config)
        {
            AppState.ServerUrl = config.ServerUrl;
            AppState.MachineToken = config.MachineToken;
            AppState.CurrentcompanyId = config.CompanyId;
            AppState.HardwareId = config.HardwareId;
            AppState.PosClientId = config.PosClientId;
            AppState.WarehouseId = config.WarehouseId;
            AppState.RegisterName = config.RegisterName;
        }
    }
}