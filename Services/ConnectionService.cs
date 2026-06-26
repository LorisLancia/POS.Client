using System;
using System.Net.Http;
using System.Threading.Tasks;
using POS.Client.Services;

namespace POS.Client.Services
{
    public class ConnectionService
    {
        private readonly HttpClient _client;

        public ConnectionService()
        {
            _client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        }

        public async Task<bool> IsServerOnlineAsync()
        {
            try
            {
                var url = AppState.ServerUrl;
                if (string.IsNullOrWhiteSpace(url))
                    url = "http://localhost:3000";

                var response = await _client.GetAsync($"{url.TrimEnd('/')}/auth/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}