using BiometricAttendanceSystem.Data;
using BiometricAttendanceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace BiometricAttendanceSystem.Repositories
{
    public class AttendanceRepository : IAttendanceRepository
    {
        private readonly AppDbContext _db;
        public AttendanceRepository(AppDbContext db) => _db = db;

        public Task<List<AttendanceRecord>> GetByDateAsync(DateTime date) =>
            _db.AttendanceRecords
               .Include(a => a.Teacher).ThenInclude(t => t!.Department)
               .Where(a => a.Date == date.Date)
               .OrderBy(a => a.Teacher!.FullName)
               .ToListAsync();

        public Task<List<AttendanceRecord>> GetByRangeAsync(DateTime from, DateTime to) =>
            _db.AttendanceRecords
               .Include(a => a.Teacher).ThenInclude(t => t!.Department)
               .Where(a => a.Date >= from.Date && a.Date <= to.Date)
               .OrderBy(a => a.Date)
               .ThenBy(a => a.Teacher!.FullName)
               .ToListAsync();

        public Task<List<AttendanceRecord>> GetByTeacherAsync(
            int teacherId, DateTime from, DateTime to) =>
            _db.AttendanceRecords
               .Where(a => a.TeacherId == teacherId
                        && a.Date >= from.Date && a.Date <= to.Date)
               .OrderByDescending(a => a.Date)
               .ToListAsync();

        public Task<List<AttendanceRecord>> GetByMonthAsync(int month, int year) =>
            _db.AttendanceRecords
               .Include(a => a.Teacher).ThenInclude(t => t!.Department)
               .Where(a => a.Date.Month == month && a.Date.Year == year)
               .ToListAsync();

        public Task<List<AttendanceRecord>> GetByDepartmentAsync(
            int deptId, DateTime from, DateTime to) =>
            _db.AttendanceRecords
               .Include(a => a.Teacher)
               .Where(a => a.Teacher!.DepartmentId == deptId
                        && a.Date >= from.Date && a.Date <= to.Date)
               .ToListAsync();

        public Task<bool> ExistsAsync(int teacherId, DateTime date) =>
            _db.AttendanceRecords
               .AnyAsync(a => a.TeacherId == teacherId && a.Date == date.Date);

        public async Task<DateTime?> GetEarliestDateAsync()
        {
            if (!await _db.AttendanceRecords.AnyAsync())
                return null;
            return await _db.AttendanceRecords.MinAsync(a => a.Date);
        }

        public async Task AddAsync(AttendanceRecord record)
        {
            _db.AttendanceRecords.Add(record);
            await _db.SaveChangesAsync();
        }

        public async Task AddRangeAsync(IEnumerable<AttendanceRecord> records)
        {
            _db.AttendanceRecords.AddRange(records);
            await _db.SaveChangesAsync();
        }
        public Task<AttendanceRecord?> GetByTeacherAndDateAsync(int teacherId, DateTime date) =>
            _db.AttendanceRecords
            .FirstOrDefaultAsync(a => a.TeacherId == teacherId && a.Date == date.Date);

        public async Task UpdateAsync(AttendanceRecord record)
        {
            _db.AttendanceRecords.Update(record);
            await _db.SaveChangesAsync();
        }
    }
}
