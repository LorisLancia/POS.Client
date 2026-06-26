using System;

namespace POS.Client.Models
{
    /// <summary>
    /// Configurazione persistente del POS client (salvata in SQLite AppConfig).
    /// </summary>
    public class SetupConfig
    {
        public string ServerUrl { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public int PosClientId { get; set; }
        public string RegisterName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string HardwareId { get; set; } = string.Empty;
        public string MachineToken { get; set; } = string.Empty;
        public DateTime ConfiguredAt { get; set; } = DateTime.UtcNow;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(ServerUrl) &&
            PosClientId > 0 &&
            !string.IsNullOrWhiteSpace(MachineToken);
    }
}