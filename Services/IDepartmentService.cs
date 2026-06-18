using BiometricAttendanceSystem.Models;

namespace BiometricAttendanceSystem.Services
{
    public interface IDepartmentService
    {
        Task<List<Department>> GetAllAsync();
        Task<Department?> GetByIdAsync(int id);

        Task<(bool Success, string Message)> CreateAsync(Department department);
        Task<(bool Success, string Message)> UpdateAsync(Department department);
        Task<(bool Success, string Message)> DeleteAsync(int id);
    }
}
