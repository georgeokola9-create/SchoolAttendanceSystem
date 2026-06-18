using BiometricAttendanceSystem.Data;
using BiometricAttendanceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace BiometricAttendanceSystem.Repositories
{
    public class TeacherRepository : ITeacherRepository
    {
        private readonly AppDbContext _db;
        public TeacherRepository(AppDbContext db) => _db = db;

        // Active teachers only
        public Task<List<Teacher>> GetAllAsync() =>
            _db.Teachers
               .Include(t => t.Department)
               .Where(t => t.IsActive)
               .OrderBy(t => t.FullName)
               .ToListAsync();

        // Active teachers only by ID
        public Task<Teacher?> GetByIdAsync(int id) =>
            _db.Teachers
               .Include(t => t.Department)
               .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

        // Any teacher regardless of active status — used for former employee details
        public Task<Teacher?> GetByIdIncludingInactiveAsync(int id) =>
            _db.Teachers
               .Include(t => t.Department)
               .FirstOrDefaultAsync(t => t.Id == id);

        // Deactivated teachers only
        public Task<List<Teacher>> GetFormerEmployeesAsync() =>
            _db.Teachers
               .Include(t => t.Department)
               .Where(t => !t.IsActive)
               .OrderBy(t => t.FullName)
               .ToListAsync();

        public Task<Teacher?> GetByNationalIdAsync(string nationalId) =>
            _db.Teachers
               .Include(t => t.Department)
               .FirstOrDefaultAsync(t => t.NationalId == nationalId);        public Task<Teacher?> GetByUsernameAsync(string username) =>
            _db.Teachers
               .Include(t => t.Department)
               .FirstOrDefaultAsync(t => t.Username == username && t.IsActive);

        public Task<List<Teacher>> SearchAsync(string query, string searchBy)
        {

            var q = _db.Teachers.Include(t => t.Department).Where(t => t.IsActive);

            q = searchBy.ToLower() switch
            {
                "nationalid" => q.Where(t => t.NationalId.Contains(query)),
                _     => q.Where(t => t.FullName.Contains(query))
            };

            return q.OrderBy(t => t.FullName).ToListAsync();
        }

        public Task<bool> NationalIdExistsAsync(string nationalId, int? excludeId = null)
        {
            var q = _db.Teachers.Where(t => t.NationalId == nationalId);
            if (excludeId.HasValue)
                q = q.Where(t => t.Id != excludeId.Value);
            return q.AnyAsync();
        }

        public Task<bool> UsernameExistsAsync(string username, int? excludeId = null)
        {
            var q = _db.Teachers.Where(t => t.Username == username);
            if (excludeId.HasValue)
                q = q.Where(t => t.Id != excludeId.Value);
            return q.AnyAsync();
        }

        public async Task AddAsync(Teacher teacher)

        {
            _db.Teachers.Add(teacher);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Teacher teacher)
        {
            _db.Teachers.Update(teacher);
            await _db.SaveChangesAsync();
        }

        public async Task DeactivateAsync(int id)
        {
            var teacher = await _db.Teachers.FindAsync(id);
            if (teacher != null)
            {
                teacher.IsActive = false;
                await _db.SaveChangesAsync();
            }
        }
    }
}
