using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace POS.Client.Services
{
    public class ApiService
    {
        private readonly RestClient _client;
        private static readonly string LogFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "POS_Client_Log.txt");

        // === MODIFICA: usa ServerUrl da AppState se disponibile, altrimenti localhost ===
        public ApiService(string baseUrl = null)
        {
            var url = baseUrl ?? AppState.ServerUrl;
            if (string.IsNullOrWhiteSpace(url))
                url = "http://localhost:3000";

            var options = new RestClientOptions(url)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            _client = new RestClient(options);
        }

        public void SetBaseUrl(string url)
        {
            // Non ricrea il client qui — il wizard crea il suo RestClient
        }

        private void Log(string message)
        {
            try
            {
                File.AppendAllText(LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
            catch { }
        }

        public void SetToken(string token)
        {
            _client.AddDefaultHeader("Authorization", $"Bearer {token}");
        }

        // ===================== ESISTENTI (non toccati) =====================
        public async Task<LoginResponse> LoginAsync(string username, string pin, int companyId)
        {
            var request = new RestRequest("/auth/login", Method.Post);
            request.AddJsonBody(new { username, pin, companyId });
            var response = await _client.ExecuteAsync(request);
            if (!response.IsSuccessful) throw new Exception(response.Content);
            return JsonConvert.DeserializeObject<LoginResponse>(response.Content);
        }

        public async Task<LoginResponse> GetMachineTokenAsync(string hardwareId, int posClientId)
        {
            var request = new RestRequest("/auth/machine-token", Method.Post);
            request.AddJsonBody(new { hardwareId, posClientId });
            var response = await _client.ExecuteAsync(request);
            if (!response.IsSuccessful) throw new Exception(response.Content);
            return JsonConvert.DeserializeObject<LoginResponse>(response.Content);
        }

        public async Task<List<ProductResponse>> GetProductsForPOSAsync(int companyId)
        {
            var request = new RestRequest($"/products/pos/{companyId}", Method.Get);
            var response = await _client.ExecuteAsync(request);
            if (!response.IsSuccessful) throw new Exception(response.Content);
            return JsonConvert.DeserializeObject<List<ProductResponse>>(response.Content);
        }

        public async Task<List<InventoryResponse>> GetInventoryForPOSAsync(int warehouseId)
        {
            var request = new RestRequest($"/materials/pos/inventory/{warehouseId}", Method.Get);
            var response = await _client.ExecuteAsync(request);
            if (!response.IsSuccessful) throw new Exception(response.Content);
            return JsonConvert.DeserializeObject<List<InventoryResponse>>(response.Content);
        }

        public async Task<SaleResponse> CreateSaleAsync(object saleDto)
        {
            var request = new RestRequest("/sales", Method.Post);
            request.AddJsonBody(saleDto);
            var response = await _client.ExecuteAsync(request);
            if (!response.IsSuccessful) throw new Exception(response.Content);
            return JsonConvert.DeserializeObject<SaleResponse>(response.Content);
        }
    }

    // ==================== DTO Models (moved from original ApiService.cs) ====================
    public class LoginResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }
        public UserResponse User { get; set; }
    }

    public class UserResponse
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public List<string> Permissions { get; set; }
    }

    public class ProductResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal BasePrice { get; set; }
        public decimal TaxRate { get; set; }
        public bool IsActive { get; set; }
        public List<VariantResponse> Variants { get; set; } = new List<VariantResponse>();
        public List<ModifierResponse> Modifiers { get; set; } = new List<ModifierResponse>();
        public List<AddonResponse> Addons { get; set; } = new List<AddonResponse>();
    }

    public class VariantResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal PriceAdjustment { get; set; }
    }

    public class ModifierResponse
    {
        public int Id { get; set; }
        public bool IsRequired { get; set; }
        public GroupResponse Group { get; set; }
    }

    public class GroupResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string SelectionType { get; set; }
        public List<OptionResponse> Options { get; set; }
    }

    public class OptionResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal PriceAdjustment { get; set; }
    }

    public class AddonResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int MaxQuantity { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<AddonItemResponse> Items { get; set; } = new List<AddonItemResponse>();
    }

    public class AddonItemResponse
    {
        public int Id { get; set; }
        public int AddonProductId { get; set; }
        public decimal QuantityValue { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
    }

    public class InventoryResponse
    {
        public int WarehouseId { get; set; }
        public int MaterialId { get; set; }
        public decimal Quantity { get; set; }
        public MaterialResponse Material { get; set; }
    }

    public class MaterialResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public UnitResponse Unit { get; set; }
    }

    public class UnitResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Symbol { get; set; }
    }

    public class SaleResponse
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("saleNumber")]
        public string SaleNumber { get; set; }

        [JsonProperty("total")]
        public decimal Total { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("shiftId")]
        public int ShiftId { get; set; }
    }

    public class ShiftResponse
    {
        public int Id { get; set; }
        public string Status { get; set; }
        public decimal StartingCash { get; set; }
        public decimal? ExpectedCash { get; set; }
        public decimal? ActualCash { get; set; }
        public decimal? Difference { get; set; }
    }

}