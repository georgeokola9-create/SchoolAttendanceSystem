using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiometricAttendanceSystem.Models
{
    public class SchoolSettings
    {
        public int Id { get; set; } = 1;

        [Required]
        [Display(Name = "School Name")]
        public string SchoolName { get; set; } = "K.P. Senior Secondary School";

        // Stored in DB — never bound from form directly
        public TimeSpan ExpectedCheckInTime { get; set; } = new(9, 0, 0);
        public TimeSpan LateArrivalTime     { get; set; } = new(10, 0, 0);

        // Form binds to these; they read/write the TimeSpan properties above
        [NotMapped]
        [Required]
        [Display(Name = "Expected Check-In Time")]
        [DataType(DataType.Time)]
        public string ExpectedCheckInTimeString
        {
            get => ExpectedCheckInTime.ToString(@"hh\:mm");
            set { if (TimeSpan.TryParse(value, out var t)) ExpectedCheckInTime = t; }
        }

        [NotMapped]
        [Required]
        [Display(Name = "Late Arrival Time")]
        [DataType(DataType.Time)]
        public string LateArrivalTimeString
        {
            get => LateArrivalTime.ToString(@"hh\:mm");
            set { if (TimeSpan.TryParse(value, out var t)) LateArrivalTime = t; }
        }

        [Display(Name = "Allow Check-In Before School Opens")]
        public bool AllowEarlyCheckIn { get; set; } = true;

        [Display(Name = "Allow Check-In After School Closes")]
        public bool AllowLateCheckIn { get; set; } = true;

        [Required]
        [Range(30, 3650, ErrorMessage = "Retention days must be between 30 and 3650.")]
        [Display(Name = "Days to Keep Attendance Records")]
        public int RetentionDays { get; set; } = 365;

        [Display(Name = "Last Updated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        public string ExpectedCheckInDisplay =>
            DateTime.Today.Add(ExpectedCheckInTime).ToString("h:mm tt");

        public string LateArrivalDisplay =>
            DateTime.Today.Add(LateArrivalTime).ToString("h:mm tt");
    }
}
