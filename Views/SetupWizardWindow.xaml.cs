using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using POS.Client.Models;
using POS.Client.Services;
using RestSharp;

namespace POS.Client.Views
{
    public partial class SetupWizardWindow : Window
    {
        private readonly ConfigService _configService;
        private string _serverUrl = string.Empty;
        private string _adminUsername = string.Empty;
        private string _adminPin = string.Empty;
        private List<CompanyDto> _companies = new();
        private List<WarehouseDto> _warehouses = new();

        public SetupWizardWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            _configService = new ConfigService();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            txtHardwareId.Text = GenerateHardwareId();

            var existing = _configService.LoadSetupConfig();
            if (existing != null)
            {
                txtServerUrl.Text = existing.ServerUrl;
                txtHardwareId.Text = existing.HardwareId;
            }
        }

        // ===================== STEP 1: Server URL =====================
        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            var url = txtServerUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Inserisci l'URL del server.", "Errore",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnConnect.IsEnabled = false;
            btnConnect.Content = "Connecting...";

            try
            {
                var client = new RestClient(new RestClientOptions(url.TrimEnd('/'))
                {
                    Timeout = TimeSpan.FromSeconds(10)
                });
                var request = new RestRequest("/auth/health", Method.Get);
                var response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful)
                {
                    MessageBox.Show("Server non raggiungibile. Verifica l'URL e riprova.",
                        "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _serverUrl = url.TrimEnd('/');
                panelStep1.Visibility = Visibility.Collapsed;
                panelStep2.Visibility = Visibility.Visible;
                txtStepDescription.Text = "Step 2 of 3: Admin Login";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore di connessione: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnConnect.IsEnabled = true;
                btnConnect.Content = "Connect";
            }
        }

        // ===================== STEP 2: Admin Login =====================
        private async void btnAdminLogin_Click(object sender, RoutedEventArgs e)
        {
            var user = txtAdminUser.Text.Trim();
            var pin = txtAdminPin.Password.Trim();

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pin))
            {
                MessageBox.Show("Inserisci username e PIN dell'amministratore.",
                    "Errore", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnAdminLogin.IsEnabled = false;
            btnAdminLogin.Content = "Verifying...";

            try
            {
                // POST /auth/admin-companies — ritorna lista company gestite dall'admin
                var client = new RestClient(new RestClientOptions(_serverUrl)
                {
                    Timeout = TimeSpan.FromSeconds(10)
                });
                var request = new RestRequest("/auth/admin-companies", Method.Post);
                request.AddJsonBody(new { username = user, pin });
                var response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful)
                {
                    var error = JsonConvert.DeserializeObject<Dictionary<string, string>>(response.Content);
                    var msg = error?.ContainsKey("message") == true ? error["message"] : "Login fallito";
                    throw new Exception(msg);
                }

                _companies = JsonConvert.DeserializeObject<List<CompanyDto>>(response.Content) ?? new List<CompanyDto>();
                if (_companies.Count == 0)
                {
                    throw new Exception("Nessuna company disponibile per questo admin");
                }

                _adminUsername = user;
                _adminPin = pin;

                // Passa allo Step 3
                panelStep2.Visibility = Visibility.Collapsed;
                panelStep3.Visibility = Visibility.Visible;
                txtStepDescription.Text = "Step 3 of 3: POS Configuration";

                // Popola dropdown company
                cmbCompany.ItemsSource = _companies;
                if (_companies.Count == 1)
                {
                    cmbCompany.SelectedIndex = 0;
                    await LoadWarehousesAsync(_companies[0].Id);
                }
                else
                {
                    cmbCompany.SelectedIndex = -1;
                    cmbWarehouse.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Login fallito: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnAdminLogin.IsEnabled = true;
                btnAdminLogin.Content = "Login as Admin";
            }
        }

        private async void cmbCompany_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbCompany.SelectedItem is not CompanyDto selected) return;
            await LoadWarehousesAsync(selected.Id);
        }

        private async Task LoadWarehousesAsync(int companyId)
        {
            try
            {
                var client = new RestClient(new RestClientOptions(_serverUrl)
                {
                    Timeout = TimeSpan.FromSeconds(10)
                });
                var request = new RestRequest($"/warehouses?companyId={companyId}", Method.Get);
                var response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful)
                {
                    _warehouses = new List<WarehouseDto>();
                    cmbWarehouse.IsEnabled = false;
                    return;
                }

                _warehouses = JsonConvert.DeserializeObject<List<WarehouseDto>>(response.Content) ?? new List<WarehouseDto>();
                cmbWarehouse.ItemsSource = _warehouses;
                cmbWarehouse.IsEnabled = _warehouses.Count > 0;
                if (_warehouses.Count > 0) cmbWarehouse.SelectedIndex = 0;
            }
            catch
            {
                _warehouses = new List<WarehouseDto>();
                cmbWarehouse.IsEnabled = false;
            }
        }

        // ===================== STEP 3: Finish =====================
        private async void btnFinish_Click(object sender, RoutedEventArgs e)
        {
            if (cmbCompany.SelectedItem is not CompanyDto company)
            {
                MessageBox.Show("Seleziona una Company.", "Errore", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbWarehouse.SelectedItem is not WarehouseDto warehouse)
            {
                MessageBox.Show("Seleziona un Warehouse.", "Errore", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var registerName = txtRegisterName.Text.Trim();
            if (string.IsNullOrWhiteSpace(registerName))
            {
                MessageBox.Show("Inserisci il Register Name.", "Errore", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var location = txtLocation.Text.Trim();
            var hardwareId = txtHardwareId.Text.Trim();

            btnFinish.IsEnabled = false;
            btnFinish.Content = "Configuring...";

            try
            {
                // Una sola chiamata: POST /pos-clients/setup
                var client = new RestClient(new RestClientOptions(_serverUrl)
                {
                    Timeout = TimeSpan.FromSeconds(30)
                });
                var request = new RestRequest("/pos-clients/setup", Method.Post);
                request.AddJsonBody(new
                {
                    adminUsername = _adminUsername,
                    adminPin = _adminPin,
                    companyId = company.Id,
                    warehouseId = warehouse.Id,
                    registerName,
                    location,
                    hardwareId
                });

                var response = await client.ExecuteAsync(request);
                if (!response.IsSuccessful)
                {
                    var error = JsonConvert.DeserializeObject<Dictionary<string, string>>(response.Content);
                    var msg = error?.ContainsKey("message") == true ? error["message"] : "Setup failed";
                    throw new Exception(msg);
                }

                var result = JsonConvert.DeserializeObject<SetupResponse>(response.Content);
                if (result == null || string.IsNullOrWhiteSpace(result.MachineToken))
                    throw new Exception("Risposta invalida dal server");

                // Salva configurazione locale
                var config = new SetupConfig
                {
                    ServerUrl = _serverUrl,
                    CompanyId = result.CompanyId,
                    CompanyName = result.CompanyName,
                    WarehouseId = result.WarehouseId,
                    WarehouseName = result.WarehouseName,
                    PosClientId = result.PosClientId,
                    RegisterName = result.RegisterName,
                    Location = location,
                    HardwareId = hardwareId,
                    MachineToken = result.MachineToken,
                    ConfiguredAt = DateTime.UtcNow
                };

                _configService.SaveSetupConfig(config);

                // Popola AppState
                AppState.ServerUrl = config.ServerUrl;
                AppState.MachineToken = config.MachineToken;
                AppState.CurrentcompanyId = config.CompanyId;
                AppState.HardwareId = config.HardwareId;
                AppState.PosClientId = config.PosClientId;
                AppState.WarehouseId = config.WarehouseId;
                AppState.RegisterName = config.RegisterName;

                MessageBox.Show("Setup completato con successo!", "Done",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore finale setup: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnFinish.IsEnabled = true;
                btnFinish.Content = "Complete Setup";
            }
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (panelStep3.Visibility == Visibility.Visible)
            {
                panelStep3.Visibility = Visibility.Collapsed;
                panelStep2.Visibility = Visibility.Visible;
                txtStepDescription.Text = "Step 2 of 3: Admin Login";
                _companies.Clear();
                _warehouses.Clear();
                cmbCompany.ItemsSource = null;
                cmbWarehouse.ItemsSource = null;
            }
            else if (panelStep2.Visibility == Visibility.Visible)
            {
                panelStep2.Visibility = Visibility.Collapsed;
                panelStep1.Visibility = Visibility.Visible;
                txtStepDescription.Text = "Step 1 of 3: Connect to Server";
            }
        }

        // ===================== Utility =====================
        private static string GenerateHardwareId()
        {
            var mac = System.Net.NetworkInformation.NetworkInterface
                .GetAllNetworkInterfaces()
                .FirstOrDefault(ni => ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                    && ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)?
                .GetPhysicalAddress().ToString();

            var machine = Environment.MachineName;
            var raw = $"{mac}-{machine}-{Environment.UserName}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash).Substring(0, 16).ToUpper();
        }
    }

    // DTO per risposta setup
    public class SetupResponse
    {
        [JsonProperty("posClientId")]
        public int PosClientId { get; set; }

        [JsonProperty("machineToken")]
        public string MachineToken { get; set; } = string.Empty;

        [JsonProperty("companyId")]
        public int CompanyId { get; set; }

        [JsonProperty("companyName")]
        public string CompanyName { get; set; } = string.Empty;

        [JsonProperty("warehouseId")]
        public int WarehouseId { get; set; }

        [JsonProperty("warehouseName")]
        public string WarehouseName { get; set; } = string.Empty;

        [JsonProperty("registerName")]
        public string RegisterName { get; set; } = string.Empty;

        [JsonProperty("hardwareId")]
        public string HardwareId { get; set; } = string.Empty;
    }

    public class CompanyDto
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class WarehouseDto
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("companyId")]
        public int CompanyId { get; set; }
    }
}