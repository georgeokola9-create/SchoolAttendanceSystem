using BiometricAttendanceSystem.Models;
using BiometricAttendanceSystem.Models.ViewModels;
using BiometricAttendanceSystem.Repositories;

namespace BiometricAttendanceSystem.Services
{
    public class AttendanceService : IAttendanceService
    {
        private readonly IAttendanceRepository _attendanceRepo;
        private readonly ITeacherRepository    _teacherRepo;
        private readonly IHolidayService       _holidays;
        private readonly ISchoolSettingsService _settingsSvc;

        public AttendanceService(
            IAttendanceRepository a,
            ITeacherRepository t,
            IHolidayService holidays,
            ISchoolSettingsService settingsSvc)
        {
            _attendanceRepo = a;
            _teacherRepo = t;
            _holidays = holidays;
            _settingsSvc = settingsSvc;
        }

        public async Task<AttendanceDashboardVM> GetDashboardAsync(DateTime? date = null)
        {
            var d = date?.Date ?? DateTime.Today;
            var records = await _attendanceRepo.GetByDateAsync(d);
            var teachers = await _teacherRepo.GetAllAsync();
            var recordedIds = records.Select(r => r.TeacherId).ToHashSet();

            var pendingRecords = teachers
                .Where(t => !recordedIds.Contains(t.Id))
                .Select(t => new AttendanceRecord
                {
                    TeacherId = t.Id,
                    Teacher = t,
                    Date = d,
                    Status = AttendanceStatus.Absent
                });

            return new AttendanceDashboardVM
            {
                Date = d,
                Records = records
                    .Concat(pendingRecords)
                    .OrderBy(r => r.Teacher!.FullName)
                    .ToList()
            };
        }

        public async Task<(bool, string)> RecordCheckInAsync(int teacherId)
        {
            if (!await _holidays.IsWorkingDayAsync(DateTime.Today))
                return (false, "Check-ins are only allowed on working days.");

            if (await _attendanceRepo.ExistsAsync(teacherId, DateTime.Today))
                return (false, "Teacher already checked in today.");

            var teacher = await _teacherRepo.GetByIdAsync(teacherId);
            if (teacher == null) return (false, "Teacher not found.");

            var settings = await _settingsSvc.GetSettingsAsync();
            var now    = DateTime.Now;
            var timeOfDay = now.TimeOfDay;
            AttendanceStatus status;
            if (timeOfDay <= settings.ExpectedCheckInTime)
                status = AttendanceStatus.Present;
            else if (timeOfDay <= settings.LateArrivalTime)
                status = AttendanceStatus.Late;
            else
                status = AttendanceStatus.Absent;

            await _attendanceRepo.AddAsync(new AttendanceRecord
            {
                TeacherId   = teacherId,
                Date        = DateTime.Today,
                CheckInTime = timeOfDay,
                Status      = status
            });

            var msg = status switch
            {
                AttendanceStatus.Late => $"{teacher.FullName} checked in LATE at {now:h:mm tt}.",
                AttendanceStatus.Absent => $"{teacher.FullName} marked ABSENT (checked in at {now:h:mm tt}).",
                _ => $"{teacher.FullName} checked in at {now:h:mm tt}."
            };

            return (true, msg);
        }

        public async Task<(bool, string)> RecordCheckInByNationalIdAsync(string nationalId)
        {
            var t = await _teacherRepo.GetByNationalIdAsync(nationalId);
            if (t == null) return (false, $"No teacher with National ID '{nationalId}'.");
            return await RecordCheckInAsync(t.Id);
        }

        public async Task<int> MarkAbsenteesAsync(DateTime date)
        {
            if (!await _holidays.IsWorkingDayAsync(date.Date))
                return 0;

            var all      = await _teacherRepo.GetAllAsync();
            var present  = await _attendanceRepo.GetByDateAsync(date.Date);
            var ids      = present.Select(r => r.TeacherId).ToHashSet();
            var absentees = all.Where(t => !ids.Contains(t.Id))
                .Select(t => new AttendanceRecord
                {
                    TeacherId = t.Id,
                    Date      = date.Date,
                    Status    = AttendanceStatus.Absent
                }).ToList();
            if (absentees.Any()) await _attendanceRepo.AddRangeAsync(absentees);
            return absentees.Count;
        }

        public async Task<TeacherHistoryVM> GetTeacherHistoryAsync(
            int teacherId, DateTime from, DateTime to)
        {
            var teacher = await _teacherRepo.GetByIdIncludingInactiveAsync(teacherId)
                ?? throw new KeyNotFoundException("Teacher not found.");
            return new TeacherHistoryVM
            {
                Teacher = teacher,
                Records = await _attendanceRepo.GetByTeacherAsync(teacherId, from, to),
                From    = from,
                To      = to
            };
        }
    }
}
