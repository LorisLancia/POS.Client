namespace POS.Client.Services
{
    public static class AppState
    {
        public static string AuthToken { get; set; } = string.Empty;      // Token cassiere (temporaneo)
        public static string MachineToken { get; set; } = string.Empty;    // Token macchina (10 anni, da SQLite)
        public static int CurrentUserId { get; set; }
        public static string CurrentUserName { get; set; } = string.Empty;
        public static int CurrentcompanyId { get; set; } = 1;
        public static int CurrentShiftId { get; set; }
        public static bool IsLoggedIn => !string.IsNullOrEmpty(AuthToken);
    }
}