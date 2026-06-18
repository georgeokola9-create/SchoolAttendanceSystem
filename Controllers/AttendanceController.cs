using BiometricAttendanceSystem.Models;
using BiometricAttendanceSystem.Repositories;
using BiometricAttendanceSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiometricAttendanceSystem.Controllers
{
    [Authorize]
    public class AttendanceController : Controller
    {
        private readonly IAttendanceService     _svc;
        private readonly IReportService         _reportSvc;
        private readonly IReportExportService   _exportSvc;
        private readonly IChartService          _chartSvc;
        private readonly IHolidayService        _holidays;
        private readonly ISchoolSettingsService _settingsSvc;
        private readonly IAttendanceRepository  _attendanceRepo;

        public AttendanceController(
            IAttendanceService svc,
            IReportService reportSvc,
            IReportExportService exportSvc,
            IChartService chartSvc,
            IHolidayService holidays,
            ISchoolSettingsService settingsSvc,
            IAttendanceRepository attendanceRepo)
        {
            _svc            = svc;
            _reportSvc      = reportSvc;
            _exportSvc      = exportSvc;
            _chartSvc       = chartSvc;
            _holidays       = holidays;
            _settingsSvc    = settingsSvc;
            _attendanceRepo = attendanceRepo;
        }

        public async Task<IActionResult> Index(DateTime? date)
        {
            var vm = await _svc.GetDashboardAsync(date);
            var settings = await _settingsSvc.GetSettingsAsync();
            ViewBag.PastAbsentThreshold    = IsPastAbsentThreshold(vm.Date, settings.LateArrivalTime);
            ViewBag.AbsentThresholdDisplay = settings.LateArrivalDisplay;
            ViewBag.CurrentTime            = DateTime.Now.ToString("h:mm:ss tt");

            // Earliest record date for dropdown bounds
            var earliest = await _attendanceRepo.GetEarliestDateAsync();
            ViewBag.EarliestDate = earliest?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd");

            var chartData = await BuildChartDataAsync(0, DateTime.Today.Month, DateTime.Today.Year, 4);
            ViewBag.Charts = await _chartSvc.GenerateChartsAsync(chartData);

            return View(vm);
        }

        // GET /Attendance/ChartData?weekOffset=0&month=6&year=2026&trendWeeks=4&pieDate=2026-06-11
        [HttpGet]
        public async Task<IActionResult> ChartData(
            int weekOffset  = 0,
            int month       = 0,
            int year        = 0,
            int trendWeeks  = 4,
            string? pieDate = null)
        {
            if (month == 0) month = DateTime.Today.Month;
            if (year  == 0) year  = DateTime.Today.Year;

            // Parse pie date
            DateTime selectedPieDate = DateTime.Today;
            if (!string.IsNullOrEmpty(pieDate) &&
                DateTime.TryParse(pieDate, out var parsedPie))
                selectedPieDate = parsedPie.Date;

            var chartData = await BuildChartDataAsync(
                weekOffset, month, year, trendWeeks, selectedPieDate);
            var result = await _chartSvc.GenerateChartsAsync(chartData);

            if (!result.Success)
                return Json(new { success = false, error = result.Error });

            return Json(new
            {
                success      = true,
                weeklyBarUrl = result.WeeklyBarUrl,
                pieUrl       = result.PieUrl,
                deptBarUrl   = result.DeptBarUrl,
                trendLineUrl = result.TrendLineUrl
            });
        }

        public async Task<IActionResult> Search(string query = "", string searchBy = "name")
            => RedirectToAction("Search", "Teacher", new { query, searchBy });

        [Authorize(Roles = "Administrator")]
        public IActionResult QrDisplay()
            => RedirectToAction("Display", "Qr");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn(int teacherId)
        {
            var (success, message) = await _svc.RecordCheckInAsync(teacherId);
            TempData[success ? "Success" : "Error"] = message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckInByNationalId(string nationalId)
        {
            var (success, message) = await _svc.RecordCheckInByNationalIdAsync(nationalId);
            TempData[success ? "Success" : "Error"] = message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAbsentees(DateTime date)
        {
            var settings = await _settingsSvc.GetSettingsAsync();

            if (date.Date == DateTime.Today && DateTime.Now.TimeOfDay < settings.LateArrivalTime)
            {
                TempData["Error"] = $"Cannot mark absentees before {settings.LateArrivalDisplay}.";
                return RedirectToAction(nameof(Index));
            }

            if (!await _holidays.IsWorkingDayAsync(date.Date))
            {
                TempData["Error"] = "Absentees can only be marked on working days.";
                return RedirectToAction(nameof(Index));
            }

            var count = await _svc.MarkAbsenteesAsync(date);
            TempData["Success"] = $"{count} teachers marked absent for {date:dd MMM yyyy}.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> History(int id, DateTime? from, DateTime? to)
        {
            var vm = await _svc.GetTeacherHistoryAsync(
                id,
                from ?? DateTime.Today.AddDays(-30),
                to   ?? DateTime.Today);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadHistoryPdf(int id, DateTime from, DateTime to)
        {
            var report = await _svc.GetTeacherHistoryAsync(id, from, to);
            var pdf    = await _exportSvc.ExportIndividualReportPdfAsync(report);
            return File(pdf, "application/pdf",
                $"Attendance_History_{report.Teacher.FullName.Replace(" ", "_")}_{DateTime.Now:yyyy-MM-dd}.pdf");
        }

        public async Task<IActionResult> PrintHistory(int id, DateTime? from, DateTime? to)
        {
            var report = await _svc.GetTeacherHistoryAsync(
                id,
                from ?? DateTime.Today.AddDays(-30),
                to   ?? DateTime.Today);
            return View("PrintHistory", report);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadHistoryExcel(int id, DateTime from, DateTime to)
        {
            var report = await _svc.GetTeacherHistoryAsync(id, from, to);
            var excel  = await _exportSvc.ExportIndividualReportExcelAsync(report);
            return File(excel,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Attendance_History_{report.Teacher.FullName.Replace(" ", "_")}_{DateTime.Now:yyyy-MM-dd}.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> LiveData()
        {
            var vm       = await _svc.GetDashboardAsync(DateTime.Today);
            var settings = await _settingsSvc.GetSettingsAsync();
            var past     = DateTime.Now.TimeOfDay >= settings.LateArrivalTime;

            var rows = vm.Records.Select(r => new
            {
                teacherId   = r.TeacherId,
                teacherName = r.Teacher!.FullName,
                nationalId  = r.Teacher.NationalId,
                department  = r.Teacher.Department?.Name,
                checkIn     = r.CheckInDisplay,
                status      = r.Status.ToString(),
                badgeColor  = r.StatusBadgeColor
            }).ToList();

            return Json(new
            {
                rows,
                presentCount    = vm.PresentCount,
                lateCount       = vm.LateCount,
                absentCount     = vm.AbsentCount,
                totalTeachers   = vm.TotalTeachers,
                percentage      = vm.AttendancePercentage,
                absentThreshold = settings.LateArrivalDisplay,
                pastAbsent      = past,
                currentTime     = DateTime.Now.ToString("h:mm:ss tt")
            });
        }

        private async Task<ChartDataVM> BuildChartDataAsync(
            int weekOffset,
            int month,
            int year,
            int trendWeekCount,
            DateTime? pieDate = null)
        {
            // Pie chart — use selected date or today
            var pieDashboard = await _svc.GetDashboardAsync(pieDate ?? DateTime.Today);

            // Weekly bar — offset from current week
            var targetWeekStart = StartOfWeek(DateTime.Today).AddDays(-7 * weekOffset);
            var weeklyPresent   = new List<int>();
            var weeklyLate      = new List<int>();
            var weeklyAbsent    = new List<int>();

            for (var i = 0; i < 5; i++)
            {
                var day = targetWeekStart.AddDays(i);
                if (day > DateTime.Today)
                {
                    weeklyPresent.Add(0);
                    weeklyLate.Add(0);
                    weeklyAbsent.Add(0);
                    continue;
                }
                var d = await _svc.GetDashboardAsync(day);
                weeklyPresent.Add(d.PresentCount);
                weeklyLate.Add(d.LateCount);
                weeklyAbsent.Add(d.AbsentCount);
            }

            // Department bar — selected month/year
            var monthly   = await _reportSvc.GetMonthlyReportAsync(month, year);
            var deptStats = monthly.Summaries
                .GroupBy(s => s.Teacher.Department?.Name ?? "Unknown")
                .Select(g => new DeptStat
                {
                    Name       = g.Key,
                    Percentage = Math.Round(g.Average(s => s.AttendancePercentage), 1)
                })
                .OrderByDescending(d => d.Percentage)
                .ToList();

            // Trend line
            var trendWeeks  = new List<string>();
            var trendValues = new List<double>();
            var thisWeekStart = StartOfWeek(DateTime.Today);

            for (var weekIdx = trendWeekCount - 1; weekIdx >= 0; weekIdx--)
            {
                var weekStart = thisWeekStart.AddDays(-7 * weekIdx);
                double total  = 0;
                var days      = 0;

                for (var di = 0; di < 5; di++)
                {
                    var day = weekStart.AddDays(di);
                    if (day > DateTime.Today) continue;
                    var dash = await _svc.GetDashboardAsync(day);
                    if (dash.TotalTeachers <= 0) continue;
                    total += dash.AttendancePercentage;
                    days++;
                }

                trendWeeks.Add($"Wk {trendWeekCount - weekIdx}");
                trendValues.Add(days > 0 ? Math.Round(total / days, 1) : 0);
            }

            return new ChartDataVM
            {
                TodayPresent  = pieDashboard.PresentCount,
                TodayLate     = pieDashboard.LateCount,
                TodayAbsent   = pieDashboard.AbsentCount,
                WeeklyPresent = weeklyPresent,
                WeeklyLate    = weeklyLate,
                WeeklyAbsent  = weeklyAbsent,
                Departments   = deptStats,
                TrendWeeks    = trendWeeks,
                TrendValues   = trendValues
            };
        }

        private static bool IsPastAbsentThreshold(DateTime dashboardDate, TimeSpan lateArrivalTime)
            => dashboardDate.Date < DateTime.Today
            || (dashboardDate.Date == DateTime.Today && DateTime.Now.TimeOfDay >= lateArrivalTime);

        private static DateTime StartOfWeek(DateTime date)
        {
            var offset = ((int)date.DayOfWeek + 6) % 7;
            return date.Date.AddDays(-offset);
        }
    }
}
