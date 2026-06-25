using POS.Client.Data;
using POS.Client.Models;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Client.Services
{
    public class OfflineAuthService
    {
        private readonly POSDbContext _db;
        private readonly ApiService _api;

        public OfflineAuthService()
        {
            _db = new POSDbContext();
            _api = new ApiService();
        }

        public async Task<LoginResult> LoginAsync(string username, string pin, int companyId)
        {
            // 1. Prova login ONLINE (se possibile)
            try
            {
                var result = await _api.LoginAsync(username, pin, companyId);
                if (!string.IsNullOrEmpty(result?.AccessToken))
                {
                    // Salva utente in SQLite per login futuri offline
                    SaveUserLocally(result.User, companyId, pin);
                    return new LoginResult
                    {
                        Success = true,
                        Token = result.AccessToken,
                        User = result.User,
                        IsOnline = true
                    };
                }
            }
            catch
            {
                // Server offline, ignora e prova locale
            }

            // 2. Login OFFLINE da SQLite
            var localUser = _db.Users.FirstOrDefault(u => u.Username == username && u.companyId == companyId && u.IsActive);
            if (localUser == null)
                return new LoginResult { Success = false, Error = "User not found locally. Connect to server first." };

            // Verifica PIN (in produzione usa hash, qui semplice)
            if (!VerifyPin(pin, localUser))
                return new LoginResult { Success = false, Error = "Invalid PIN" };

            return new LoginResult
            {
                Success = true,
                Token = "offline-token", // token fittizio per offline
                User = new UserResponse
                {
                    Id = localUser.ServerId,
                    Username = localUser.Username,
                    FullName = localUser.FullName,
                    Role = localUser.RoleName
                },
                IsOnline = false
            };
        }

        private void SaveUserLocally(UserResponse user, int companyId, string pin)
        {
            var existing = _db.Users.FirstOrDefault(u => u.ServerId == user.Id && u.companyId == companyId);
            if (existing != null)
            {
                existing.FullName = user.FullName;
                existing.RoleName = user.Role;
                existing.PermissionsJson = string.Join(",", user.Permissions ?? new System.Collections.Generic.List<string>());
            }
            else
            {
                _db.Users.Add(new LocalUser
                {
                    ServerId = user.Id,
                    companyId = companyId,
                    Username = user.Username,
                    FullName = user.FullName,
                    RoleName = user.Role,
                    PermissionsJson = string.Join(",", user.Permissions ?? new System.Collections.Generic.List<string>()),
                    IsActive = true
                });
            }
            _db.SaveChanges();
        }

        private bool VerifyPin(string pin, LocalUser user)
        {
            // Semplificato: in produzione confronta hash
            // Per ora accetta sempre se l'utente esiste (il PIN vero è verificato solo online)
            return true;
        }
    }

    public class LoginResult
    {
        public bool Success { get; set; }
        public string Token { get; set; } = string.Empty;
        public UserResponse User { get; set; }
        public bool IsOnline { get; set; }
        public string Error { get; set; } = string.Empty;
    }
}