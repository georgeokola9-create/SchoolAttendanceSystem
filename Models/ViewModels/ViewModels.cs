using System.ComponentModel.DataAnnotations;
using System.Globalization;
using BiometricAttendanceSystem.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BiometricAttendanceSystem.Models.ViewModels
{
    // public class LoginVM
    // {
    //     [Required(ErrorMessage = "Username is required")]
    //     public string Username { get; set; } = string.Empty;

    //     [Required(ErrorMessage = "Password is required")]
    //     [DataType(DataType.Password)]
    //     public string Password { get; set; } = string.Empty;

    //     public bool RememberMe { get; set; }
    // }

    public class AttendanceDashboardVM
    {
        public DateTime Date { get; set; } = DateTime.Today;
        public List<AttendanceRecord> Records { get; set; } = new();
        public int PresentCount  => Records.Count(r => r.Status == AttendanceStatus.Present);
        public int LateCount     => Records.Count(r => r.Status == AttendanceStatus.Late);
        public int AbsentCount   => Records.Count(r => r.Status == AttendanceStatus.Absent);
        public int TotalTeachers => Records.Count;
        public double AttendancePercentage =>
            TotalTeachers == 0 ? 0
            : Math.Round((double)(PresentCount + LateCount) / TotalTeachers * 100, 1);
    }

    public class TeacherSearchVM
    {
        public string? Query    { get; set; }
        public string SearchBy  { get; set; } = "name";
        public List<Teacher> Results { get; set; } = new();
    }

    public class TeacherHistoryVM
    {
        public Teacher Teacher  { get; set; } = null!;
        public List<AttendanceRecord> Records { get; set; } = new();
        public DateTime From    { get; set; }
        public DateTime To      { get; set; }
        public int TotalDays    => Records.Count;
        public int PresentDays  => Records.Count(r => r.Status == AttendanceStatus.Present);
        public int LateDays     => Records.Count(r => r.Status == AttendanceStatus.Late);
        public int AbsentDays   => Records.Count(r => r.Status == AttendanceStatus.Absent);
        public double AttendancePercentage =>
            TotalDays == 0 ? 0
            : Math.Round((double)(PresentDays + LateDays) / TotalDays * 100, 1);
    }

    // Alias so existing print views using IndividualReportVM still compile
    public class IndividualReportVM : TeacherHistoryVM { }

    public class DailyReportVM
    {
        public DateTime Date { get; set; } = DateTime.Today;
        public List<AttendanceRecord> Records { get; set; } = new();

        // Computed properties used by print and report views
        public int PresentCount => Records.Count(r => r.Status == AttendanceStatus.Present);
        public int LateCount    => Records.Count(r => r.Status == AttendanceStatus.Late);
        public int AbsentCount  => Records.Count(r => r.Status == AttendanceStatus.Absent);
        public int TotalCount   => Records.Count;
        public double AttendancePercentage =>
            TotalCount == 0 ? 0
            : Math.Round((double)(PresentCount + LateCount) / TotalCount * 100, 1);
    }

    public class MonthlyReportVM
    {
        public const int StartYear = 2026;

        public int Month { get; set; } = DateTime.Today.Month;
        public int Year  { get; set; } = DateTime.Today.Year;
        public List<SelectListItem> MonthOptions { get; set; } = new();
        public List<MonthlyTeacherSummary> Summaries { get; set; } = new();
        public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
        public bool IsCurrentMonth =>
            Month == DateTime.Today.Month && Year == DateTime.Today.Year;
        public string PeriodLabel =>
            IsCurrentMonth
                ? $"Month-to-date as of {DateTime.Today:dd MMM yyyy}"
                : "Full month";
    }

    public class MonthlyTeacherSummary
    {
        public Teacher Teacher     { get; set; } = null!;
        public int PresentDays     { get; set; }
        public int LateDays        { get; set; }
        public int AbsentDays      { get; set; }
        public int WorkingDays     { get; set; }
        public int FullMonthWorkingDays { get; set; }
        public double AttendancePercentage =>
            WorkingDays == 0 ? 0
            : Math.Round((double)(PresentDays + LateDays) / WorkingDays * 100, 1);
    }

    public class WeeklyReportVM
    {
        public static readonly DateTime SystemStartDate = new(2026, 1, 1);

        public int WeekYear { get; set; }
        public int WeekNumber { get; set; }
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
        public List<AttendanceRecord> Records { get; set; } = new();

        public string WeekLabel => $"{WeekStart:dd MMM yyyy} to {WeekEnd:dd MMM yyyy}";
        public string WorkingWeekLabel => $"{WeekStart:dd MMM yyyy} to {WeekEnd:dd MMM yyyy} (Mon-Fri)";
        public string WeekInputValue => $"{WeekYear}-W{WeekNumber:D2}";
        public string MinimumWeekValue => $"{ISOWeek.GetYear(SystemStartDate)}-W{ISOWeek.GetWeekOfYear(SystemStartDate):D2}";
        public string CurrentWeekValue => $"{ISOWeek.GetYear(DateTime.Today)}-W{ISOWeek.GetWeekOfYear(DateTime.Today):D2}";
        public bool IsMinimumWeek =>
            WeekYear == ISOWeek.GetYear(SystemStartDate) &&
            WeekNumber == ISOWeek.GetWeekOfYear(SystemStartDate);
        public string PreviousWeekValue
        {
            get
            {
                if (IsMinimumWeek) return WeekInputValue;
                var previous = WeekStart.AddDays(-7);
                return $"{ISOWeek.GetYear(previous)}-W{ISOWeek.GetWeekOfYear(previous):D2}";
            }
        }
        public string NextWeekValue
        {
            get
            {
                if (WeekInputValue == CurrentWeekValue) return WeekInputValue;
                var next = WeekStart.AddDays(7);
                return $"{ISOWeek.GetYear(next)}-W{ISOWeek.GetWeekOfYear(next):D2}";
            }
        }
        public bool IsCurrentWeek =>
            WeekYear == ISOWeek.GetYear(DateTime.Today) &&
            WeekNumber == ISOWeek.GetWeekOfYear(DateTime.Today);
        public int PresentCount => Records.Count(r => r.Status == AttendanceStatus.Present);
        public int LateCount    => Records.Count(r => r.Status == AttendanceStatus.Late);
        public int AbsentCount  => Records.Count(r => r.Status == AttendanceStatus.Absent);
        public int TotalCount   => Records.Count;
        public double AttendancePercentage =>
            TotalCount == 0 ? 0 : Math.Round((double)(PresentCount + LateCount) / TotalCount * 100, 1);
    }

    public class DepartmentReportVM
    {
        public Department Department { get; set; } = null!;
        public DateTime From  { get; set; }
        public DateTime To    { get; set; }
        public List<MonthlyTeacherSummary> Summaries { get; set; } = new();
    }

    public class TeacherRegisterVM
    {
        public Teacher Teacher              { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
    }
    public class RegisterDevicePromptVM
        {
            public string Token   { get; set; } = "";
            public string Message { get; set; } = "";
        }
}
