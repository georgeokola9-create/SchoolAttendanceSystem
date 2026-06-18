using BiometricAttendanceSystem.Data;
using BiometricAttendanceSystem.Models;
using BiometricAttendanceSystem.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BiometricAttendanceSystem.Repositories
{
    public interface IHolidayRepository
    {
        Task<List<Holiday>> GetAllAsync();
        Task<Holiday?> GetByIdAsync(int id);
        Task<List<Holiday>> GetByRangeAsync(DateTime from, DateTime to);
        Task AddAsync(Holiday holiday);
        Task UpdateAsync(Holiday holiday);
        Task DeleteAsync(int id);
    }

    public class HolidayRepository : IHolidayRepository
    {
        private readonly AppDbContext _db;
        public HolidayRepository(AppDbContext db) => _db = db;

        public Task<List<Holiday>> GetAllAsync() =>
            _db.Holidays.OrderBy(h => h.StartDate).ToListAsync();

        public Task<Holiday?> GetByIdAsync(int id) =>
            _db.Holidays.FirstOrDefaultAsync(h => h.Id == id);

        public Task<List<Holiday>> GetByRangeAsync(DateTime from, DateTime to) =>
            _db.Holidays
               .Where(h => h.StartDate <= to.Date && h.EndDate >= from.Date)
               .OrderBy(h => h.StartDate)
               .ToListAsync();

        public async Task AddAsync(Holiday holiday)
        {
            _db.Holidays.Add(holiday);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Holiday holiday)
        {
            _db.Holidays.Update(holiday);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var holiday = await _db.Holidays.FindAsync(id);
            if (holiday != null)
            {
                _db.Holidays.Remove(holiday);
                await _db.SaveChangesAsync();
            }
        }
    }
}

namespace BiometricAttendanceSystem.Services
{
    public interface IHolidayService
    {
        Task<List<Holiday>> GetAllHolidaysAsync();
        Task<Holiday?> GetHolidayByIdAsync(int id);
        Task<bool> IsHolidayAsync(DateTime date);
        Task<bool> IsWorkingDayAsync(DateTime date);
        Task<int> CountWorkingDaysAsync(DateTime from, DateTime to);
        Task<(bool Success, string Message)> AddHolidayAsync(Holiday holiday);
        Task<(bool Success, string Message)> UpdateHolidayAsync(Holiday holiday);
        Task<bool> DeleteHolidayAsync(int id);
    }

    public class HolidayService : IHolidayService
    {
        private readonly IHolidayRepository _repo;
        private List<Holiday>? _cachedHolidays;

        public HolidayService(IHolidayRepository repo) => _repo = repo;

        public async Task<List<Holiday>> GetAllHolidaysAsync()
        {
            _cachedHolidays ??= await _repo.GetAllAsync();
            return _cachedHolidays;
        }

        public Task<Holiday?> GetHolidayByIdAsync(int id) =>
            _repo.GetByIdAsync(id);

        public async Task<bool> IsHolidayAsync(DateTime date)
        {
            var holidays = await GetAllHolidaysAsync();
            return holidays.Any(h => h.IncludesDate(date));
        }

        public async Task<bool> IsWorkingDayAsync(DateTime date)
        {
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                return false;

            return !await IsHolidayAsync(date);
        }

        public async Task<int> CountWorkingDaysAsync(DateTime from, DateTime to)
        {
            if (from.Date > to.Date)
                return 0;

            var holidays = await GetAllHolidaysAsync();
            int count = 0;

            for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)
                    continue;

                if (holidays.Any(h => h.IncludesDate(d)))
                    continue;

                count++;
            }

            return count;
        }

        public async Task<(bool Success, string Message)> AddHolidayAsync(Holiday holiday)
        {
            if (holiday.EndDate < holiday.StartDate)
                return (false, "End date must be after start date.");

            await _repo.AddAsync(holiday);
            _cachedHolidays = null;
            return (true, $"{holiday.Name} added successfully.");
        }

        public async Task<(bool Success, string Message)> UpdateHolidayAsync(Holiday holiday)
        {
            if (holiday.EndDate < holiday.StartDate)
                return (false, "End date must be after start date.");

            await _repo.UpdateAsync(holiday);
            _cachedHolidays = null;
            return (true, "Holiday updated successfully.");
        }

        public async Task<bool> DeleteHolidayAsync(int id)
        {
            await _repo.DeleteAsync(id);
            _cachedHolidays = null;
            return true;
        }
    }
}
