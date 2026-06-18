using System.ComponentModel.DataAnnotations;

namespace BiometricAttendanceSystem.Models
{
    public enum AttendanceStatus { Present, Late, Absent }

    public class AttendanceRecord
    {
        public int Id { get; set; }

        [Required]
        public int TeacherId { get; set; }
        public Teacher? Teacher { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Display(Name = "Check-In Time")]
        public TimeSpan? CheckInTime { get; set; }

        public string? Notes { get; set; }

        [Required]
        public AttendanceStatus Status { get; set; }

        public string CheckInDisplay =>
            CheckInTime.HasValue
                ? DateTime.Today.Add(CheckInTime.Value).ToString("h:mm tt")
                : "-";

        public string StatusBadgeColor => Status switch
        {
            AttendanceStatus.Present => "success",
            AttendanceStatus.Late => "warning",
            AttendanceStatus.Absent => "danger",
            _ => "secondary"
        };
    }
}
