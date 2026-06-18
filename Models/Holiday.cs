using System.ComponentModel.DataAnnotations;

namespace BiometricAttendanceSystem.Models
{
    public class Holiday
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Holiday Name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        [Display(Name = "Description")]
        [StringLength(500)]
        public string? Description { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public bool IncludesDate(DateTime date)
        {
            return date.Date >= StartDate.Date && date.Date <= EndDate.Date;
        }
    }
}
