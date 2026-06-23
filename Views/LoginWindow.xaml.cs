using System.Windows;
using POS.Client.Services;

namespace POS.Client.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var username = txtUsername.Text;
                var pin = txtPin.Password;
                var storeId = int.Parse(txtStoreId.Text);

                var auth = new OfflineAuthService();
                var result = await auth.LoginAsync(username, pin, storeId);

                if (!result.Success)
                {
                    txtMessage.Text = result.Error;
                    return;
                }

                // Token utente per operare il POS (temporaneo, fino a chiusura app)
                AppState.AuthToken = result.Token;
                AppState.CurrentUserId = result.User.Id;
                AppState.CurrentUserName = result.User.FullName;
                AppState.CurrentStoreId = storeId;

                // Se login online, ottieni anche MACHINE TOKEN (10 anni)
                if (result.IsOnline)
                {
                    try
                    {
                        var api = new ApiService();
                        var machine = await api.GetMachineTokenAsync("DUMMY-HARDWARE-ID-001", 1);
                        var config = new ConfigService();
                        config.SaveToken(machine.AccessToken);
                        AppState.MachineToken = machine.AccessToken;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Machine token error: {ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                string mode = result.IsOnline ? "ONLINE" : "OFFLINE";
                MessageBox.Show(
                    $"Welcome {result.User.FullName}!\nMode: {mode}",
                    "Login Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                this.Close();
            }
            catch (Exception ex)
            {
                txtMessage.Text = ex.Message;
            }
        }
    }
}