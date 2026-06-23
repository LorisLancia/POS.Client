using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using POS.Client.Services;
using POS.Client.Views;

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

            // Carica MachineToken per sync
            var machineToken = _config.GetToken();
            if (!string.IsNullOrEmpty(machineToken))
            {
                AppState.MachineToken = machineToken;
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
            if (_isSyncing) return; // Evita sovrapposizioni
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
                await _syncService.SyncProductsAsync(1);
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

            // Mostra risultato finale
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
                    MessageBox.Show($"{pending} sale(s) still pending.\nCheck log file for details.", "Sync Incomplete", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}