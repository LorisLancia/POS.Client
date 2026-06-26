using POS.Client.Data;
using POS.Client.Models;
using Newtonsoft.Json;

namespace POS.Client.Services
{
    public class ConfigService
    {
        private readonly POSDbContext _db;

        public ConfigService()
        {
            _db = new POSDbContext();
        }

        // === Token utente (cassiere) — ESISTENTE ===
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

        // === NUOVO: SetupConfig (POS wizard) ===
        public SetupConfig LoadSetupConfig()
        {
            var config = _db.AppConfigs.FirstOrDefault(c => c.Key == "pos_setup");
            if (config == null || string.IsNullOrWhiteSpace(config.Value))
                return null;

            try
            {
                return JsonConvert.DeserializeObject<SetupConfig>(config.Value);
            }
            catch
            {
                return null;
            }
        }

        public void SaveSetupConfig(SetupConfig config)
        {
            var json = JsonConvert.SerializeObject(config);
            var existing = _db.AppConfigs.FirstOrDefault(c => c.Key == "pos_setup");
            if (existing != null)
            {
                existing.Value = json;
                existing.UpdatedAt = DateTime.Now;
            }
            else
            {
                _db.AppConfigs.Add(new AppConfig
                {
                    Key = "pos_setup",
                    Value = json,
                    UpdatedAt = DateTime.Now
                });
            }
            _db.SaveChanges();
        }

        public void ClearSetupConfig()
        {
            var config = _db.AppConfigs.FirstOrDefault(c => c.Key == "pos_setup");
            if (config != null)
            {
                _db.AppConfigs.Remove(config);
                _db.SaveChanges();
            }
        }

        public bool IsConfigured => LoadSetupConfig()?.IsValid ?? false;
    }
}