using BiometricAttendanceSystem.Models;

namespace BiometricAttendanceSystem.Models.ViewModels
{
    public class FormerEmployeeDetailVM
    {
        public Teacher Teacher { get; set; } = null!;
        public List<AttendanceRecord> Records { get; set; } = new();

        public int PresentDays => Records.Count(r => r.Status == AttendanceStatus.Present);
        public int LateDays    => Records.Count(r => r.Status == AttendanceStatus.Late);
        public int AbsentDays  => Records.Count(r => r.Status == AttendanceStatus.Absent);
        public int TotalDays   => Records.Count;
    }
}
