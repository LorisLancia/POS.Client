using System;
using System.Net.Http;
using System.Threading.Tasks;

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
                var response = await _client.GetAsync("http://localhost:3000/auth/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}