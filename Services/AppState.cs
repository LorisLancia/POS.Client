namespace POS.Client.Services
{
    public static class AppState
    {
        // === ESISTENTI (non toccati) ===
        public static string AuthToken { get; set; } = string.Empty;
        public static string MachineToken { get; set; } = string.Empty;
        public static int CurrentUserId { get; set; }
        public static string CurrentUserName { get; set; } = string.Empty;
        public static int CurrentcompanyId { get; set; } = 1;
        public static int CurrentShiftId { get; set; }
        public static bool IsLoggedIn => !string.IsNullOrEmpty(AuthToken);

        // === NUOVI: Configurazione POS (da setup wizard) ===
        public static string ServerUrl { get; set; } = string.Empty;
        public static string HardwareId { get; set; } = string.Empty;
        public static int PosClientId { get; set; }
        public static int WarehouseId { get; set; }
        public static string RegisterName { get; set; } = string.Empty;
    }
}