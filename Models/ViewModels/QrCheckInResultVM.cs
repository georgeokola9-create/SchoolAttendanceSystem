namespace BiometricAttendanceSystem.Models.ViewModels
{
    public class QrCheckInResultVM
    {
        public bool   Success    { get; set; }
        public string Message    { get; set; } = string.Empty;
        public string Status     { get; set; } = string.Empty;   // "Present" | "Late" | ""
        public string CheckInTime { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;

        // Failure reason codes for the view to show the right icon/colour
        public string? FailureReason { get; set; }
        // "InvalidToken" | "WifiBlocked" | "UnknownDevice" | "AlreadyCheckedIn" | "NotFound"
    }
}

