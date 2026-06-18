namespace BiometricAttendanceSystem.Models
{
    public class DeviceReregistrationRequest
    {
        public int      Id          { get; set; }
        public int      TeacherId   { get; set; }
        public Teacher? Teacher     { get; set; }

        public string   RequestedIp { get; set; } = "";
        public DateTime RequestedAt { get; set; } = DateTime.Now;

        public ReregistrationStatus Status { get; set; } = ReregistrationStatus.Pending;

        public DateTime? ReviewedAt { get; set; }
        public string?   ReviewNote { get; set; }

        // Temporarily holds the new device ID until approved
        public string   PendingDeviceId { get; set; } = "";
    }

    public enum ReregistrationStatus
    {
        Pending,
        Approved,
        Rejected
    }
}
