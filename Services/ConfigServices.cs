using POS.Client.Data;
using POS.Client.Models;

namespace POS.Client.Services
{
    public class ConfigService
    {
        private readonly POSDbContext _db;

        public ConfigService()
        {
            _db = new POSDbContext();
        }

        public string GetToken()
        {
            var config = _db.AppConfigs.FirstOrDefault(c => c.Key == "auth_token");
            return config?.Value ?? string.Empty;
        }

        public void SaveToken(string token)
        {
            var config = _db.AppConfigs.FirstOrDefault(c => c.Key == "auth_token");
            if (config != null)
            {
                config.Value = token;
                config.UpdatedAt = DateTime.Now;
            }
            else
            {
                _db.AppConfigs.Add(new AppConfig
                {
                    Key = "auth_token",
                    Value = token,
                    UpdatedAt = DateTime.Now
                });
            }
            _db.SaveChanges();
        }

        public void ClearToken()
        {
            var config = _db.AppConfigs.FirstOrDefault(c => c.Key == "auth_token");
            if (config != null)
            {
                _db.AppConfigs.Remove(config);
                _db.SaveChanges();
            }
        }
    }
}