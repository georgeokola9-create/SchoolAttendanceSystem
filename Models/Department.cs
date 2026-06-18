using System.ComponentModel.DataAnnotations;

namespace BiometricAttendanceSystem.Models
{
    public class Department
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Department Name")]
        public string Name { get; set; } = string.Empty;

        public ICollection<Teacher> Teachers { get; set; }
            = new List<Teacher>();
    }
}
