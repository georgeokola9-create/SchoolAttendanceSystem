using BiometricAttendanceSystem.Models;
using BiometricAttendanceSystem.Repositories;

namespace BiometricAttendanceSystem.Services
{
    public class DepartmentService : IDepartmentService
    {
        private readonly IDepartmentRepository _repo;
        public DepartmentService(IDepartmentRepository repo) => _repo = repo;

        public Task<List<Department>> GetAllAsync() =>
            _repo.GetAllAsync();

        public Task<Department?> GetByIdAsync(int id) =>
            _repo.GetByIdAsync(id);

        public async Task<(bool Success, string Message)> CreateAsync(Department department)
        {
            if (string.IsNullOrWhiteSpace(department.Name))
                return (false, "Department name is required.");

            department.Name = department.Name.Trim();

            if (await _repo.NameExistsAsync(department.Name))
                return (false, $"A department named '{department.Name}' already exists.");

            await _repo.AddAsync(department);
            return (true, $"Department '{department.Name}' created successfully.");
        }

        public async Task<(bool Success, string Message)> UpdateAsync(Department department)
        {
            if (string.IsNullOrWhiteSpace(department.Name))
                return (false, "Department name is required.");

            var existing = await _repo.GetByIdAsync(department.Id);
            if (existing == null)
                return (false, "Department not found.");

            department.Name = department.Name.Trim();

            if (await _repo.NameExistsAsync(department.Name, excludeId: department.Id))
                return (false, $"A department named '{department.Name}' already exists.");

            existing.Name = department.Name;
            await _repo.UpdateAsync(existing);
            return (true, $"Department '{existing.Name}' updated successfully.");
        }

        public async Task<(bool Success, string Message)> DeleteAsync(int id)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null)
                return (false, "Department not found.");

            var totalDepartments = await _repo.GetCountAsync();
            if (totalDepartments <= 1)
                return (false, "Cannot delete the only remaining department. At least one department must exist so teachers can be registered.");

            var teacherCount = await _repo.GetTeacherCountAsync(id);
            if (teacherCount > 0)
            {
                var noun = teacherCount == 1 ? "teacher" : "teachers";
                return (false, $"Cannot delete '{existing.Name}' — {teacherCount} {noun} assigned to it. Reassign them first.");
            }

            await _repo.DeleteAsync(id);
            return (true, $"Department '{existing.Name}' deleted successfully.");
        }
    }
}
