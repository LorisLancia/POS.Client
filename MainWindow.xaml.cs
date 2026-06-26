using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using POS.Client.Services;
using POS.Client.Views;
using RestSharp;

namespace POS.Client
{
    public partial class MainWindow : Window
    {
        private readonly SyncService _syncService;
        private readonly ConnectionService _connection;
        private readonly ConfigService _config;
        private DispatcherTimer _syncTimer;
        private DispatcherTimer _connectionTimer;
        private bool _isSyncing = false;

        public MainWindow()
        {
            InitializeComponent();
            _syncService = new SyncService();
            _connection = new ConnectionService();
            _config = new ConfigService();

            // RESET login utente ad ogni avvio
            AppState.AuthToken = string.Empty;
            AppState.CurrentUserId = 0;
            AppState.CurrentUserName = string.Empty;
            AppState.CurrentShiftId = 0;

            // Carica MachineToken per sync (da setup config)
            var setupConfig = _config.LoadSetupConfig();
            if (setupConfig != null && !string.IsNullOrEmpty(setupConfig.MachineToken))
            {
                AppState.MachineToken = setupConfig.MachineToken;
            }

            _connectionTimer = new DispatcherTimer();
            _connectionTimer.Interval = TimeSpan.FromSeconds(5);
            _connectionTimer.Tick += (s, e) => CheckConnection();
            _connectionTimer.Start();

            _syncTimer = new DispatcherTimer();
            _syncTimer.Interval = TimeSpan.FromMinutes(1);
            _syncTimer.Tick += async (s, e) => await BackgroundSync();
            _syncTimer.Start();

            CheckConnection();

            if (!string.IsNullOrEmpty(AppState.MachineToken))
            {
                _ = BackgroundSync();
            }
        }

        private async void CheckConnection()
        {
            bool online = await _connection.IsServerOnlineAsync();
            ellipseStatus.Fill = new SolidColorBrush(online ? Colors.LimeGreen : Colors.Red);
            txtConnection.Text = online ? "Online" : "Offline";
            txtConnection.Foreground = new SolidColorBrush(online ? Colors.LimeGreen : Colors.Red);
            UpdatePendingAlert();
        }

        private void UpdatePendingAlert()
        {
            try
            {
                var token = AppState.MachineToken;
                var queue = new OfflineQueueService(token);
                int pending = queue.GetPendingCount();
                txtPending.Visibility = pending > 0 ? Visibility.Visible : Visibility.Collapsed;
                txtPending.Text = $"⚠ {pending} sale(s) pending sync";
            }
            catch { }
        }

        private async Task BackgroundSync()
        {
            if (_isSyncing) return;
            _isSyncing = true;

            try
            {
                var token = AppState.MachineToken;
                if (string.IsNullOrEmpty(token))
                {
                    txtStatus.Text = "Machine token missing";
                    return;
                }

                var queue = new OfflineQueueService(token);
                int pendingBefore = queue.GetPendingCount();

                if (pendingBefore == 0)
                {
                    txtStatus.Text = "No pending sales";
                    return;
                }

                txtStatus.Text = $"Syncing {pendingBefore} sales...";

                int synced = await queue.TrySyncPendingSalesAsync();
                int pendingAfter = queue.GetPendingCount();

                if (synced > 0)
                {
                    txtStatus.Text = $"Synced {synced} sales ({pendingAfter} remaining)";
                    UpdatePendingAlert();
                }
                else if (pendingAfter < pendingBefore)
                {
                    txtStatus.Text = $"Some sales failed ({pendingAfter} remaining)";
                    UpdatePendingAlert();
                }
                else
                {
                    txtStatus.Text = $"Server offline or busy ({pendingAfter} pending)";
                }
            }
            catch (Exception ex)
            {
                string msg = ex.Message ?? "";
                if (msg.Contains("401") || msg.Contains("Unauthorized"))
                {
                    _config.ClearToken();
                    AppState.MachineToken = string.Empty;
                    txtStatus.Text = "Token expired - login required";
                }
                else
                {
                    txtStatus.Text = $"Sync error: {msg}";
                }
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.ShowDialog();
            UpdatePendingAlert();
        }

        private void btnCashier_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.IsLoggedIn)
            {
                MessageBox.Show("Please login first to operate the POS!", "Login Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var cashierWindow = new CashierWindow();
            cashierWindow.Show();
        }

        private async void btnSync_Click(object sender, RoutedEventArgs e)
        {
            txtStatus.Text = "Syncing products...";
            try
            {
                await _syncService.SyncProductsAsync(AppState.CurrentcompanyId);
                txtStatus.Text = $"Products: {_syncService.GetProducts().Count}";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Sync error: {ex.Message}";
            }
        }

        private async void btnSyncSales_Click(object sender, RoutedEventArgs e)
        {
            txtStatus.Text = "Force syncing sales...";
            await BackgroundSync();

            var token = AppState.MachineToken;
            if (!string.IsNullOrEmpty(token))
            {
                var queue = new OfflineQueueService(token);
                int pending = queue.GetPendingCount();
                if (pending == 0)
                {
                    MessageBox.Show("All sales synced successfully!", "Sync Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"{pending} sale(s) still pending. Check log file for details.", "Sync Incomplete", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

              // === MODIFICATO: Reconfigure POS ===
        private async void btnReconfigure_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will deactivate this POS on the server and delete the local configuration.\n" +
                "You will need to run the setup wizard again.\n\nContinue?",
                "Reconfigure POS",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            // Step 1: Verifica se il server è online
            bool isServerOnline = await _connection.IsServerOnlineAsync();

            // Step 2: Se online, disattiva il POS sul server
            bool serverDeactivated = false;
            if (isServerOnline 
                && !string.IsNullOrEmpty(AppState.MachineToken) 
                && AppState.PosClientId > 0 
                && !string.IsNullOrEmpty(AppState.ServerUrl))
            {
                try
                {
                    using (var httpClient = new System.Net.Http.HttpClient())
                    {
                        httpClient.BaseAddress = new Uri(AppState.ServerUrl);
                        httpClient.DefaultRequestHeaders.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AppState.MachineToken);
                        
                        var response = await httpClient.PostAsync(
                            $"pos-clients/{AppState.PosClientId}/self-deactivate", 
                            new System.Net.Http.StringContent(""));
                        
                        if (response.IsSuccessStatusCode)
                        {
                            serverDeactivated = true;
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            serverDeactivated = true;
                        }
                        else
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            MessageBox.Show(
                                $"Server warning: {content}\n\nProceeding with local cleanup.",
                                "Server Warning",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Could not reach server to deactivate POS: {ex.Message}\n\n" +
                        "Proceeding with local cleanup only.",
                        "Connection Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            else if (!isServerOnline)
            {
                var continueWithoutServer = MessageBox.Show(
                    "The server is currently unavailable.\n\n" +
                    "If you continue, the local POS configuration will be cleared, the current connection settings will be removed, and you will need to run the setup wizard again when the backend becomes reachable.\n\n" +
                    "This action will also interrupt any pending server-side deactivation process.\n\n" +
                    "Do you want to continue anyway?",
                    "Server Offline",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (continueWithoutServer != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            // Step 3: Cleanup locale completo
            _config.ClearSetupConfig();
            _config.ClearToken();
            
            AppState.MachineToken = string.Empty;
            AppState.ServerUrl = string.Empty;
            AppState.CurrentcompanyId = 0;
            AppState.HardwareId = string.Empty;
            AppState.PosClientId = 0;
            AppState.WarehouseId = 0;
            AppState.RegisterName = string.Empty;
            AppState.AuthToken = string.Empty;
            AppState.CurrentUserId = 0;
            AppState.CurrentUserName = string.Empty;
            AppState.CurrentShiftId = 0;

            // Step 4: Apri wizard (modale su questa MainWindow)
            var wizard = new SetupWizardWindow();
            bool? ok = wizard.ShowDialog();

            if (ok != true)
            {
                // L'utente ha annullato: mantieni la MainWindow corrente aperta.
                this.Activate();
                return;
            }

            // Step 5: Ricarica config
            var config = _config.LoadSetupConfig();
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

            // Step 6: Apri nuova MainWindow PRIMA di chiudere la vecchia.
            // In questo modo l'applicazione mantiene una finestra principale valida.
            var newMain = new MainWindow();
            Application.Current.MainWindow = newMain;
            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            newMain.Show();

            // Step 7: Chiudi la vecchia MainWindow
            this.Close();
        }
    }
}