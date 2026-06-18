using BiometricAttendanceSystem.Repositories;
using BiometricAttendanceSystem.Services;
using BiometricAttendanceSystem.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Globalization;

namespace BiometricAttendanceSystem.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly IReportService        _svc;
        private readonly IReportExportService  _exportSvc;
        private readonly IDepartmentRepository _deptRepo;
        private readonly ITeacherRepository    _teacherRepo;
        private readonly IHolidayService       _holidaySvc;

        public ReportsController(
            IReportService svc,
            IReportExportService exportSvc,
            IDepartmentRepository deptRepo,
            ITeacherRepository teacherRepo,
            IHolidayService holidaySvc)
        {
            _svc         = svc;
            _exportSvc   = exportSvc;
            _deptRepo    = deptRepo;
            _teacherRepo = teacherRepo;
            _holidaySvc  = holidaySvc;
        }

        // GET /Reports
        public IActionResult Index() => View();

        // GET /Reports/Daily
        public async Task<IActionResult> Daily(DateTime? date)
        {
            var vm = await _svc.GetDailyReportAsync(date ?? DateTime.Today);
            return View(vm);
        }

        // GET /Reports/Weekly
        public async Task<IActionResult> Weekly(string? week)
        {
            var (weekYear, weekNumber) = ResolveWeek(week);
            var vm = await _svc.GetWeeklyReportAsync(weekYear, weekNumber);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadDailyPdf(DateTime? date)
        {
            var report = await _svc.GetDailyReportAsync(date ?? DateTime.Today);
            var pdf = await _exportSvc.ExportDailyReportPdfAsync(report);
            return File(pdf, "application/pdf", $"Daily_Report_{report.Date:yyyy-MM-dd}.pdf");
        }

        // GET /Reports/PrintDaily
        public async Task<IActionResult> PrintDaily(DateTime? date)
        {
            var report = await _svc.GetDailyReportAsync(date ?? DateTime.Today);
            var pdf = await _exportSvc.ExportDailyReportPdfAsync(report);
            return File(pdf, "application/pdf", $"Daily_Report_{report.Date:yyyy-MM-dd}.pdf");
        }

        // GET /Reports/PrintDailyPage
        public async Task<IActionResult> PrintDailyPage(DateTime? date)
        {
            var report = await _svc.GetDailyReportAsync(date ?? DateTime.Today);
            return View("PrintDaily", report);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadDailyExcel(DateTime? date)
        {
            var report = await _svc.GetDailyReportAsync(date ?? DateTime.Today);
            var excel = await _exportSvc.ExportDailyReportExcelAsync(report);
            return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Daily_Report_{report.Date:yyyy-MM-dd}.xlsx");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadWeeklyPdf(string week)
        {
            var (weekYear, weekNumber) = ResolveWeek(week);
            var report = await _svc.GetWeeklyReportAsync(weekYear, weekNumber);
            var pdf = await _exportSvc.ExportWeeklyReportPdfAsync(report);
            return File(pdf, "application/pdf", $"Weekly_Report_{report.WeekStart:yyyy-MM-dd}_to_{report.WeekEnd:yyyy-MM-dd}.pdf");
        }

        // GET /Reports/PrintWeekly
        public async Task<IActionResult> PrintWeekly(string? week)
        {
            var (weekYear, weekNumber) = ResolveWeek(week);
            var report = await _svc.GetWeeklyReportAsync(weekYear, weekNumber);
            var pdf = await _exportSvc.ExportWeeklyReportPdfAsync(report);
            return File(pdf, "application/pdf", $"Weekly_Report_{report.WeekStart:yyyy-MM-dd}_to_{report.WeekEnd:yyyy-MM-dd}.pdf");
        }

        // GET /Reports/PrintWeeklyPage
        public async Task<IActionResult> PrintWeeklyPage(string? week)
        {
            var (weekYear, weekNumber) = ResolveWeek(week);
            var report = await _svc.GetWeeklyReportAsync(weekYear, weekNumber);
            return View("PrintWeekly", report);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadWeeklyExcel(string week)
        {
            var (weekYear, weekNumber) = ResolveWeek(week);
            var report = await _svc.GetWeeklyReportAsync(weekYear, weekNumber);
            var excel = await _exportSvc.ExportWeeklyReportExcelAsync(report);
            return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Weekly_Report_{report.WeekStart:yyyy-MM-dd}_to_{report.WeekEnd:yyyy-MM-dd}.xlsx");
        }

        // GET /Reports/Monthly
        public async Task<IActionResult> Monthly(int? month, int? year)
        {
            var currentYear = DateTime.Today.Year;
            var selectedYear = Math.Clamp(year ?? currentYear, MonthlyReportVM.StartYear, currentYear);
            var monthOptions = await BuildMonthOptionsAsync(selectedYear);
            var defaultMonth = selectedYear == currentYear
                ? DateTime.Today.Month
                : monthOptions.LastOrDefault() is { Value: string lastValue } && int.TryParse(lastValue, out var lastMonth)
                    ? lastMonth
                    : 1;

            var selectedMonth = month ?? defaultMonth;
            if (monthOptions.Count > 0 && !monthOptions.Any(m => m.Value == selectedMonth.ToString()))
            {
                selectedMonth = int.TryParse(monthOptions.Last().Value, out var fallbackMonth)
                    ? fallbackMonth
                    : defaultMonth;
            }

            var vm = await _svc.GetMonthlyReportAsync(selectedMonth, selectedYear);
            vm.Month = selectedMonth;
            vm.Year = selectedYear;
            vm.MonthOptions = monthOptions;
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadMonthlyPdf(int month, int year)
        {
            var report = await _svc.GetMonthlyReportAsync(month, year);
            var pdf = await _exportSvc.ExportMonthlyReportPdfAsync(report);
            return File(pdf, "application/pdf", $"Monthly_Report_{report.MonthName.Replace(" ", "_")}.pdf");
        }

        // GET /Reports/PrintMonthly
        public async Task<IActionResult> PrintMonthly(int? month, int? year)
        {
            var currentYear = DateTime.Today.Year;
            var selectedYear = Math.Clamp(year ?? currentYear, MonthlyReportVM.StartYear, currentYear);
            var monthOptions = await BuildMonthOptionsAsync(selectedYear);

            var defaultMonth = selectedYear == currentYear
                ? DateTime.Today.Month
                : monthOptions.LastOrDefault() is { Value: string lastValue } && int.TryParse(lastValue, out var lastMonth)
                    ? lastMonth
                    : 1;

            var selectedMonth = month ?? defaultMonth;
            if (monthOptions.Count > 0 && !monthOptions.Any(m => m.Value == selectedMonth.ToString()))
            {
                selectedMonth = int.TryParse(monthOptions.Last().Value, out var fallbackMonth)
                    ? fallbackMonth
                    : defaultMonth;
            }

            var report = await _svc.GetMonthlyReportAsync(selectedMonth, selectedYear);
            var pdf = await _exportSvc.ExportMonthlyReportPdfAsync(report);
            return File(pdf, "application/pdf", $"Monthly_Report_{report.MonthName.Replace(" ", "_")}.pdf");
        }

        // GET /Reports/PrintMonthlyPage
        public async Task<IActionResult> PrintMonthlyPage(int? month, int? year)
        {
            var currentYear = DateTime.Today.Year;
            var selectedYear = Math.Clamp(year ?? currentYear, MonthlyReportVM.StartYear, currentYear);
            var monthOptions = await BuildMonthOptionsAsync(selectedYear);

            var defaultMonth = selectedYear == currentYear
                ? DateTime.Today.Month
                : monthOptions.LastOrDefault() is { Value: string lastValue } && int.TryParse(lastValue, out var lastMonth)
                    ? lastMonth
                    : 1;

            var selectedMonth = month ?? defaultMonth;
            if (monthOptions.Count > 0 && !monthOptions.Any(m => m.Value == selectedMonth.ToString()))
            {
                selectedMonth = int.TryParse(monthOptions.Last().Value, out var fallbackMonth)
                    ? fallbackMonth
                    : defaultMonth;
            }

            var report = await _svc.GetMonthlyReportAsync(selectedMonth, selectedYear);
            report.Month = selectedMonth;
            report.Year = selectedYear;

            return View("PrintMonthly", report);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadMonthlyExcel(int month, int year)
        {
            var report = await _svc.GetMonthlyReportAsync(month, year);
            var excel = await _exportSvc.ExportMonthlyReportExcelAsync(report);
            return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Monthly_Report_{report.MonthName.Replace(" ", "_")}.xlsx");
        }

        // GET /Reports/Individual
        public async Task<IActionResult> Individual(
            int? teacherId, DateTime? from, DateTime? to)
        {
            ViewBag.Teachers = await _teacherRepo.GetAllAsync();

            if (teacherId == null) return View();

            var vm = await _svc.GetIndividualReportAsync(
                teacherId.Value,
                from ?? DateTime.Today.AddMonths(-1),
                to   ?? DateTime.Today);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadIndividualPdf(int teacherId, DateTime from, DateTime to)
        {
            var report = await _svc.GetIndividualReportAsync(teacherId, from, to);
            var pdf = await _exportSvc.ExportIndividualReportPdfAsync(report);
            return File(pdf, "application/pdf",
                $"Individual_Report_{report.Teacher.FullName.Replace(" ", "_")}_{DateTime.Now:yyyy-MM-dd}.pdf");
        }

        // GET /Reports/PrintIndividual
        public async Task<IActionResult> PrintIndividual(int? teacherId, DateTime? from, DateTime? to)
        {
            if (teacherId == null) return BadRequest("teacherId is required.");

            var report = await _svc.GetIndividualReportAsync(
                teacherId.Value,
                from ?? DateTime.Today.AddMonths(-1),
                to ?? DateTime.Today);

            var pdf = await _exportSvc.ExportIndividualReportPdfAsync(report);
            return File(pdf, "application/pdf",
                $"Individual_Report_{report.Teacher.FullName.Replace(" ", "_")}_{DateTime.Now:yyyy-MM-dd}.pdf");
        }

        // GET /Reports/PrintIndividualPage
        public async Task<IActionResult> PrintIndividualPage(int? teacherId, DateTime? from, DateTime? to)
        {
            if (teacherId == null) return BadRequest("teacherId is required.");

            var report = await _svc.GetIndividualReportAsync(
                teacherId.Value,
                from ?? DateTime.Today.AddMonths(-1),
                to ?? DateTime.Today);

            return View("PrintIndividual", report);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadIndividualExcel(int teacherId, DateTime from, DateTime to)
        {
            var report = await _svc.GetIndividualReportAsync(teacherId, from, to);
            var excel = await _exportSvc.ExportIndividualReportExcelAsync(report);
            return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Individual_Report_{report.Teacher.FullName.Replace(" ", "_")}_{DateTime.Now:yyyy-MM-dd}.xlsx");
        }

        // GET /Reports/Department
        public async Task<IActionResult> Department(
            int? deptId, DateTime? from, DateTime? to)
        {
            ViewBag.Departments = await _deptRepo.GetAllAsync();

            if (deptId == null) return View();

            var vm = await _svc.GetDepartmentReportAsync(
                deptId.Value,
                from ?? DateTime.Today.AddMonths(-1),
                to   ?? DateTime.Today);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadDepartmentPdf(int deptId, DateTime from, DateTime to)
        {
            var report = await _svc.GetDepartmentReportAsync(deptId, from, to);
            var pdf = await _exportSvc.ExportDepartmentReportPdfAsync(report);
            return File(pdf, "application/pdf",
                $"Department_Report_{report.Department.Name.Replace(" ", "_")}_{DateTime.Now:yyyy-MM-dd}.pdf");
        }

        // GET /Reports/PrintDepartment
        public async Task<IActionResult> PrintDepartment(int? deptId, DateTime? from, DateTime? to)
        {
            if (deptId == null) return BadRequest("deptId is required.");

            var report = await _svc.GetDepartmentReportAsync(
                deptId.Value,
                from ?? DateTime.Today.AddMonths(-1),
                to ?? DateTime.Today);

            var pdf = await _exportSvc.ExportDepartmentReportPdfAsync(report);
            return File(pdf, "application/pdf",
                $"Department_Report_{report.Department.Name.Replace(" ", "_")}_{DateTime.Now:yyyy-MM-dd}.pdf");
        }

        // GET /Reports/PrintDepartmentPage
        public async Task<IActionResult> PrintDepartmentPage(int? deptId, DateTime? from, DateTime? to)
        {
            if (deptId == null) return BadRequest("deptId is required.");

            var report = await _svc.GetDepartmentReportAsync(
                deptId.Value,
                from ?? DateTime.Today.AddMonths(-1),
                to ?? DateTime.Today);

            return View("PrintDepartment", report);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadDepartmentExcel(int deptId, DateTime from, DateTime to)
        {
            var report = await _svc.GetDepartmentReportAsync(deptId, from, to);
            var excel = await _exportSvc.ExportDepartmentReportExcelAsync(report);
            return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Department_Report_{report.Department.Name.Replace(" ", "_")}_{DateTime.Now:yyyy-MM-dd}.xlsx");
        }

        private async Task<List<SelectListItem>> BuildMonthOptionsAsync(int year)
        {
            var maxMonth = year == DateTime.Today.Year ? DateTime.Today.Month : 12;
            var options = new List<SelectListItem>();

            for (var month = 1; month <= maxMonth; month++)
            {
                var start = new DateTime(year, month, 1);
                var end = start.AddMonths(1).AddDays(-1);
                var workingDays = await _holidaySvc.CountWorkingDaysAsync(start, end);

                if (workingDays == 0)
                    continue;

                options.Add(new SelectListItem
                {
                    Value = month.ToString(),
                    Text = start.ToString("MMMM")
                });
            }

            return options;
        }

        private static (int Year, int WeekNumber) ResolveWeek(string? week)
        {
            var minStart = WeeklyReportVM.SystemStartDate.Date;
            var maxStart = StartOfWeek(DateTime.Today);

            if (string.IsNullOrWhiteSpace(week))
                return (ISOWeek.GetYear(maxStart), ISOWeek.GetWeekOfYear(maxStart));

            var parts = week.Split("-W", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], out var year) || !int.TryParse(parts[1], out var weekNumber))
                return (ISOWeek.GetYear(maxStart), ISOWeek.GetWeekOfYear(maxStart));

            var maxWeek = ISOWeek.GetWeeksInYear(year);
            if (weekNumber < 1 || weekNumber > maxWeek)
                return (ISOWeek.GetYear(maxStart), ISOWeek.GetWeekOfYear(maxStart));

            var selectedStart = ISOWeek.ToDateTime(year, weekNumber, DayOfWeek.Monday).Date;
            if (selectedStart < minStart)
                return (ISOWeek.GetYear(minStart), ISOWeek.GetWeekOfYear(minStart));

            if (selectedStart > maxStart)
                return (ISOWeek.GetYear(maxStart), ISOWeek.GetWeekOfYear(maxStart));

            return (year, weekNumber);
        }

        private static DateTime StartOfWeek(DateTime date)
        {
            var offset = ((int)date.DayOfWeek + 6) % 7;
            return date.Date.AddDays(-offset);
        }
    }
}
