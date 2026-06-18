using BiometricAttendanceSystem.Data;
using BiometricAttendanceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace BiometricAttendanceSystem.Repositories
{
    public interface ISchoolSettingsRepository
    {
        Task<SchoolSettings> GetSettingsAsync();
        Task UpdateSettingsAsync(SchoolSettings settings);
    }

    public class SchoolSettingsRepository : ISchoolSettingsRepository
    {
        private readonly AppDbContext _db;
        public SchoolSettingsRepository(AppDbContext db) => _db = db;

        public async Task<SchoolSettings> GetSettingsAsync()
        {
            var settings = await _db.SchoolSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new SchoolSettings
                {
                    SchoolName          = "K.P. Senior Secondary School",
                    ExpectedCheckInTime = new TimeSpan(9, 0, 0),
                    LateArrivalTime     = new TimeSpan(10, 0, 0),
                    AllowEarlyCheckIn   = true,
                    AllowLateCheckIn    = true,
                    RetentionDays       = 365,
                    LastUpdated         = DateTime.UtcNow
                };
                _db.SchoolSettings.Add(settings);
                await _db.SaveChangesAsync();
            }

            return settings;
        }

        public async Task UpdateSettingsAsync(SchoolSettings settings)
        {
            settings.LastUpdated = DateTime.UtcNow;
            _db.SchoolSettings.Update(settings);
            await _db.SaveChangesAsync();
        }
    }
}

namespace BiometricAttendanceSystem.Services
{
    public interface ISchoolSettingsService
    {
        Task<SchoolSettings> GetSettingsAsync();
        Task<(bool Success, string Message)> UpdateSettingsAsync(SchoolSettings settings);
    }

    public class SchoolSettingsService : ISchoolSettingsService
    {
        private readonly BiometricAttendanceSystem.Repositories.ISchoolSettingsRepository _repo;

        public SchoolSettingsService(
            BiometricAttendanceSystem.Repositories.ISchoolSettingsRepository repo)
            => _repo = repo;

        public async Task<SchoolSettings> GetSettingsAsync()
            => await _repo.GetSettingsAsync();

        public async Task<(bool Success, string Message)> UpdateSettingsAsync(SchoolSettings incoming)
        {
            if (incoming.LateArrivalTime <= incoming.ExpectedCheckInTime)
                return (false, "Late Arrival time must be after Expected Check-In time.");

            if (incoming.RetentionDays < 30 || incoming.RetentionDays > 3650)
                return (false, "Retention days must be between 30 and 3650.");

            // Fetch the tracked entity from DB so EF Core updates the existing row
            var existing = await _repo.GetSettingsAsync();
            existing.SchoolName          = incoming.SchoolName;
            existing.ExpectedCheckInTime = incoming.ExpectedCheckInTime;
            existing.LateArrivalTime     = incoming.LateArrivalTime;
            existing.AllowEarlyCheckIn   = incoming.AllowEarlyCheckIn;
            existing.AllowLateCheckIn    = incoming.AllowLateCheckIn;
            existing.RetentionDays       = incoming.RetentionDays;

            await _repo.UpdateSettingsAsync(existing);
            return (true, "School settings updated successfully.");
        }
    }
}
