using BiometricAttendanceSystem.Data;
using BiometricAttendanceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace BiometricAttendanceSystem.Repositories
{
    public class DepartmentRepository : IDepartmentRepository
    {
        private readonly AppDbContext _db;
        public DepartmentRepository(AppDbContext db) => _db = db;

        public async Task<List<Department>> GetAllAsync()
        {
            var departments = await _db.Departments
                .OrderBy(d => d.Name)
                .ToListAsync();

            if (departments.Count == 0)
            {
                // Small schools may have no real departmental structure.
                // Seed one default department so the dropdown is never empty
                // and teachers can still be registered.
                var defaultDept = new Department { Name = "General" };
                _db.Departments.Add(defaultDept);
                await _db.SaveChangesAsync();
                departments.Add(defaultDept);
            }

            return departments;
        }

        public Task<Department?> GetByIdAsync(int id) =>
            _db.Departments
               .FirstOrDefaultAsync(d => d.Id == id);

        public async Task AddAsync(Department department)
        {
            _db.Departments.Add(department);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Department department)
        {
            _db.Departments.Update(department);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var dept = await _db.Departments.FindAsync(id);
            if (dept == null) return;
            _db.Departments.Remove(dept);
            await _db.SaveChangesAsync();
        }

        public Task<bool> NameExistsAsync(string name, int? excludeId = null)
        {
            var normalized = name.Trim().ToLower();
            return _db.Departments
                .Where(d => excludeId == null || d.Id != excludeId)
                .AnyAsync(d => d.Name.ToLower() == normalized);
        }

        public Task<bool> IsInUseAsync(int departmentId) =>
            _db.Teachers.AnyAsync(t => t.DepartmentId == departmentId);

        public Task<int> GetTeacherCountAsync(int departmentId) =>
            _db.Teachers.CountAsync(t => t.DepartmentId == departmentId);

        public Task<int> GetCountAsync() =>
            _db.Departments.CountAsync();
    }
}
