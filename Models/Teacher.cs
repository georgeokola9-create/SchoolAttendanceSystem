using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiometricAttendanceSystem.Models
{
    public class Teacher
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "National ID is required")]
        [StringLength(20)]
        [Display(Name = "National ID")]
        public string NationalId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Department")]
        public int DepartmentId { get; set; }

        public Department? Department { get; set; }

        [Display(Name = "Fingerprint Template")]
        public string? FingerprintTemplate { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Date Registered")]
        public DateTime DateRegistered { get; set; } = DateTime.Now;

        // ── Teacher login credentials ──────────────────────────────────────
        [StringLength(50)]
        [Display(Name = "Username")]
        public string? Username { get; set; }

        /// <summary>BCrypt hash of the teacher's password. Null = no login set up yet.</summary>
        public string? PasswordHash { get; set; }

        [Display(Name = "Credentials Created")]
        public DateTime? CredentialsCreatedAt { get; set; }

        [Display(Name = "Password Changed")]
        public bool HasChangedPassword { get; set; } = false;

        // ── QR / device check-in ───────────────────────────────────────────
        [StringLength(100)]
        [Display(Name = "Registered Device ID")]
        public string? RegisteredDeviceId { get; set; }

        [Display(Name = "Device Registered At")]
        public DateTime? DeviceRegisteredAt { get; set; }

        [StringLength(50)]
        [Display(Name = "Device Registered IP")]
        public string? DeviceRegisteredIp { get; set; }

        // ── Contact ───────────────────────────────────────────────────────────
        [StringLength(150)]
        [Display(Name = "Email Address")]
        [EmailAddress]
        public string? Email { get; set; }

        public ICollection<AttendanceRecord> AttendanceRecords { get; set; }
            = new List<AttendanceRecord>();
    }
}

