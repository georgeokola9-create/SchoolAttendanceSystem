using BiometricAttendanceSystem.Models;

namespace BiometricAttendanceSystem.Repositories
{
    public interface ITeacherRepository
    {
        Task<List<Teacher>>  GetAllAsync();
        Task<Teacher?>       GetByIdAsync(int id);
        Task<Teacher?>       GetByIdIncludingInactiveAsync(int id);
        Task<List<Teacher>>  GetFormerEmployeesAsync();
        Task<Teacher?>       GetByNationalIdAsync(string nationalId);
        Task<Teacher?>       GetByUsernameAsync(string username);
        Task<List<Teacher>>  SearchAsync(string query, string searchBy);
        Task<bool>           NationalIdExistsAsync(string nationalId, int? excludeId = null);
        Task<bool>           UsernameExistsAsync(string username, int? excludeId = null);
        Task                 AddAsync(Teacher teacher);
        Task                 UpdateAsync(Teacher teacher);
        Task                 DeactivateAsync(int id);
    }


    public interface IAttendanceRepository
    {
        Task<List<AttendanceRecord>> GetByDateAsync(DateTime date);
        Task<List<AttendanceRecord>> GetByRangeAsync(DateTime from, DateTime to);
        Task<List<AttendanceRecord>> GetByTeacherAsync(int teacherId, DateTime from, DateTime to);
        Task<List<AttendanceRecord>> GetByMonthAsync(int month, int year);
        Task<List<AttendanceRecord>> GetByDepartmentAsync(int deptId, DateTime from, DateTime to);
        Task<bool>                   ExistsAsync(int teacherId, DateTime date);
        Task<DateTime?>              GetEarliestDateAsync();
        Task                         AddAsync(AttendanceRecord record);
        Task                         AddRangeAsync(IEnumerable<AttendanceRecord> records);
        Task<AttendanceRecord?> GetByTeacherAndDateAsync(int teacherId, DateTime date);
Task                    UpdateAsync(AttendanceRecord record);
    }

    public interface IDepartmentRepository
    {
        Task<List<Department>> GetAllAsync();
        Task<Department?>      GetByIdAsync(int id);

        Task AddAsync(Department department);
        Task UpdateAsync(Department department);
        Task DeleteAsync(int id);

        /// <summary>Case-insensitive check for an existing department name. Pass excludeId when editing to ignore the record being edited.</summary>
        Task<bool> NameExistsAsync(string name, int? excludeId = null);

        /// <summary>True if any teacher is currently assigned to this department.</summary>
        Task<bool> IsInUseAsync(int departmentId);

        /// <summary>Count of teachers currently assigned to this department (for messaging).</summary>
        Task<int> GetTeacherCountAsync(int departmentId);

        /// <summary>Total count of departments (used to prevent deleting the last remaining department).</summary>
        Task<int> GetCountAsync();
    }
}
