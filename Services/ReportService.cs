using BiometricAttendanceSystem.Models;
using BiometricAttendanceSystem.Models.ViewModels;
using BiometricAttendanceSystem.Repositories;
using System.Globalization;

namespace BiometricAttendanceSystem.Services
{
    public class ReportService : IReportService
    {
        private readonly IAttendanceRepository _a;
        private readonly ITeacherRepository    _t;
        private readonly IDepartmentRepository _d;
        private readonly IHolidayService       _holidays;

        public ReportService(
            IAttendanceRepository a,
            ITeacherRepository t,
            IDepartmentRepository d,
            IHolidayService holidays)
        { _a = a; _t = t; _d = d; _holidays = holidays; }

        public async Task<DailyReportVM> GetDailyReportAsync(DateTime date) =>
            new() { Date = date.Date, Records = await _a.GetByDateAsync(date.Date) };

        public async Task<WeeklyReportVM> GetWeeklyReportAsync(int weekYear, int weekNumber)
        {
            var weekStart = ISOWeek.ToDateTime(weekYear, weekNumber, DayOfWeek.Monday).Date;
            var weekEnd = weekStart.AddDays(4);
            var holidays = await _holidays.GetAllHolidaysAsync();
            var records = await _a.GetByRangeAsync(weekStart, weekEnd);

            return new WeeklyReportVM
            {
                WeekYear = weekYear,
                WeekNumber = weekNumber,
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                Records = records
                    .Where(r => IsWorkingDay(r.Date, holidays))
                    .OrderBy(r => r.Date)
                    .ThenBy(r => r.Teacher!.FullName)
                    .ToList()
            };
        }

        public async Task<MonthlyReportVM> GetMonthlyReportAsync(int month, int year)
        {
            var records = await _a.GetByMonthAsync(month, year);
            var teachers = await _t.GetAllAsync();
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var reportEnd = (month == DateTime.Today.Month && year == DateTime.Today.Year)
                ? DateTime.Today.Date
                : monthEnd;
            var summaries = new List<MonthlyTeacherSummary>();

            foreach (var teacher in teachers)
            {
                summaries.Add(await BuildSummaryAsync(
                    teacher,
                    records.Where(r => r.TeacherId == teacher.Id).ToList(),
                    monthStart,
                    monthEnd,
                    reportEnd));
            }

            return new MonthlyReportVM
            {
                Month = month, Year = year,
                Summaries = summaries
            };
        }

        public async Task<DepartmentReportVM> GetDepartmentReportAsync(
            int deptId, DateTime from, DateTime to)
        {
            var dept = await _d.GetByIdAsync(deptId)
                ?? throw new KeyNotFoundException("Department not found.");
            var records  = await _a.GetByDepartmentAsync(deptId, from, to);
            var teachers = (await _t.GetAllAsync()).Where(t => t.DepartmentId == deptId).ToList();
            var summaries = new List<MonthlyTeacherSummary>();

            foreach (var teacher in teachers)
            {
                summaries.Add(await BuildSummaryAsync(
                    teacher,
                    records.Where(r => r.TeacherId == teacher.Id).ToList(),
                    from,
                    to));
            }

            return new DepartmentReportVM
            {
                Department = dept, From = from, To = to,
                Summaries  = summaries
            };
        }

        public async Task<TeacherHistoryVM> GetIndividualReportAsync(
            int teacherId, DateTime from, DateTime to)
        {
            var teacher = await _t.GetByIdAsync(teacherId)
                ?? throw new KeyNotFoundException("Teacher not found.");
            return new TeacherHistoryVM
            {
                Teacher = teacher,
                Records = await _a.GetByTeacherAsync(teacherId, from, to),
                From = from, To = to
            };
        }

        private async Task<MonthlyTeacherSummary> BuildSummaryAsync(
            Teacher teacher,
            List<AttendanceRecord> records,
            DateTime from,
            DateTime to,
            DateTime? reportEnd = null)
        {
            var effectiveFrom = teacher.DateRegistered.Date > from.Date
                ? teacher.DateRegistered.Date
                : from.Date;
            var reportEndDate = reportEnd?.Date ?? to.Date;
            var fullMonthWorkingDays = await _holidays.CountWorkingDaysAsync(effectiveFrom, to.Date);
            var workingDays = await _holidays.CountWorkingDaysAsync(effectiveFrom, reportEndDate);
            var holidays = await _holidays.GetAllHolidaysAsync();
            var effectiveRecords = records
                .Where(r => r.Date.Date >= effectiveFrom && r.Date.Date <= reportEndDate)
                .Where(r => IsWorkingDay(r.Date, holidays))
                .GroupBy(r => r.Date.Date)
                .Select(g => g.OrderByDescending(r => r.Id).First())
                .ToList();

            return new MonthlyTeacherSummary
            {
                Teacher     = teacher,
                PresentDays = effectiveRecords.Count(r => r.Status == AttendanceStatus.Present),
                LateDays    = effectiveRecords.Count(r => r.Status == AttendanceStatus.Late),
                AbsentDays  = effectiveRecords.Count(r => r.Status == AttendanceStatus.Absent),
                WorkingDays = workingDays,
                FullMonthWorkingDays = fullMonthWorkingDays
            };
        }

        private static bool IsWorkingDay(DateTime date, List<Holiday> holidays)
        {
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                return false;

            return !holidays.Any(h => h.IncludesDate(date));
        }
    }
}
