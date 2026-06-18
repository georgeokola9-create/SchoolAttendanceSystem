using System.Security.Claims;
using BiometricAttendanceSystem.Models;
using BiometricAttendanceSystem.Models.ViewModels;
using BiometricAttendanceSystem.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiometricAttendanceSystem.Controllers
{
    [Authorize]
    public class TeacherController : Controller
    {
        private readonly ITeacherService      _teacherService;
        private readonly IAttendanceService   _attendanceService;
        private readonly ITeacherAuthService  _authService;
        private readonly IEmailService        _emailService;
        private readonly ILocalNetworkService _localNetwork;
        private readonly ISchoolSettingsService _schoolSettings;
        private readonly IReportExportService _reportExportService;

        public TeacherController(
            ITeacherService teacherService,
            IAttendanceService attendanceService,
            ITeacherAuthService authService,
            IEmailService emailService,
            ILocalNetworkService localNetwork,
            ISchoolSettingsService schoolSettings,
            IReportExportService reportExportService)
        {
            _teacherService    = teacherService;
            _attendanceService = attendanceService;
            _authService       = authService;
            _emailService      = emailService;
            _localNetwork      = localNetwork;
            _schoolSettings    = schoolSettings;
            _reportExportService = reportExportService;
        }

        // ── Admin-only: teacher management ───────────────────────────────

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Index()
        {
            var teachers = await _teacherService.GetAllTeachersAsync();
            return View(teachers);
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Register()
        {
            var vm = new TeacherRegisterVM
            {
                Departments = await _teacherService.GetDepartmentsAsync()
            };
            return View(vm);
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(TeacherRegisterVM vm)
        {
            if (!ModelState.IsValid)
            {
                vm.Departments = await _teacherService.GetDepartmentsAsync();
                return View(vm);
            }

            var (success, message) = await _teacherService.RegisterTeacherAsync(vm.Teacher);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                vm.Departments = await _teacherService.GetDepartmentsAsync();
                return View(vm);
            }

            // Auto-set credentials for the new teacher and redirect to credentials page
            var newTeacher = await _teacherService.GetAllTeachersAsync();
            var created    = newTeacher.FirstOrDefault(t => t.NationalId == vm.Teacher.NationalId);

            if (created != null)
            {
                var (credOk, plain, _) = await _authService.SetCredentialsAsync(created.Id);
                if (credOk)
                {
                    TempData["Success"]       = $"Credentials set. Password: {plain}";
                    TempData["LastTeacherId"] = created.Id;
                    TempData["LastPlain"]     = plain;
                    TempData["NewTeacher"]    = created.FullName;

                    // Email PDF slip if teacher has an email address
                    if (!string.IsNullOrWhiteSpace(created.Email))
                    {
                        var scheme   = Request.Scheme;
                        var host     = Request.Host;
                        var localIp  = _localNetwork.GetPreferredIPv4Address();
                        var loginUrl = localIp != null
                            ? $"{scheme}://{localIp}:{host.Port ?? 5000}/Account/Login"
                            : $"{scheme}://{host}/Account/Login";

                        var payload  = $"{loginUrl}|{plain}";
                        var pdfBytes = await _authService.GenerateCredentialSlipAsync(created.Id, payload);
                        var safeName = created.FullName.ToLower().Replace(" ", "-");
                        var fileName = $"credentials-{safeName}.pdf";

                        var schoolSettings = await _schoolSettings.GetSettingsAsync();
                        var (emailOk, emailMsg) = await _emailService.SendCredentialSlipAsync(
                            created.Email, created.FullName, pdfBytes, fileName, schoolSettings.SchoolName);

                        TempData["EmailStatus"] = emailOk
                            ? $"Credential slip emailed to {created.Email}."
                            : $"Email failed: {emailMsg}";
                    }

                    return RedirectToAction("Index", "Credentials");
                }
            }

            TempData["Success"] = message;
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Details(int id)
        {
            var teacher = await _teacherService.GetTeacherByIdAsync(id);
            if (teacher == null) return NotFound();
            return View(teacher);
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Edit(int id)
        {
            var teacher = await _teacherService.GetTeacherByIdAsync(id);
            if (teacher == null) return NotFound();
            var vm = new TeacherRegisterVM
            {
                Teacher     = teacher,
                Departments = await _teacherService.GetDepartmentsAsync()
            };
            return View(vm);
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TeacherRegisterVM vm)
        {
            if (!ModelState.IsValid)
            {
                vm.Departments = await _teacherService.GetDepartmentsAsync();
                return View(vm);
            }

            var (success, message) = await _teacherService.UpdateTeacherAsync(vm.Teacher);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                vm.Departments = await _teacherService.GetDepartmentsAsync();
                return View(vm);
            }

            TempData["Success"] = message;
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> ConfirmDeactivate(int id)
        {
            var teacher = await _teacherService.GetTeacherByIdAsync(id);
            if (teacher == null) return NotFound();
            return View(teacher);
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            await _teacherService.DeactivateTeacherAsync(id);
            TempData["Success"] = "Teacher deactivated.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Search(string query = "", string searchBy = "name")
        {
            var vm = await _teacherService.SearchTeachersAsync(query, searchBy);
            return View(vm);
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> FormerEmployees()
        {
            var teachers = await _teacherService.GetFormerEmployeesAsync();
            return View(teachers);
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> FormerDetails(int id, DateTime? from, DateTime? to)
        {
            var teacher = await _teacherService.GetTeacherByIdIncludingInactiveAsync(id);
            if (teacher == null) return NotFound();

            // Build history VM so the view gets the right model type
            var dateFrom = from ?? new DateTime(DateTime.Today.Year, 1, 1);
            var dateTo   = to   ?? DateTime.Today;

            var vm = await _attendanceService.GetTeacherHistoryAsync(id, dateFrom, dateTo);
            return View(vm);
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> PrintFormerDetails(int id, DateTime? from, DateTime? to)
        {
            var dateFrom = from ?? new DateTime(DateTime.Today.Year, 1, 1);
            var dateTo   = to   ?? DateTime.Today;

            var vm = await _attendanceService.GetTeacherHistoryAsync(id, dateFrom, dateTo);
            return View("~/Views/Attendance/PrintHistory.cshtml", vm);
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadFormerEmployeePdf(int id, DateTime? from, DateTime? to)
        {
            var dateFrom = from ?? new DateTime(DateTime.Today.Year, 1, 1);
            var dateTo   = to   ?? DateTime.Today;

            var vm = await _attendanceService.GetTeacherHistoryAsync(id, dateFrom, dateTo);
            var pdfBytes = await _reportExportService.ExportIndividualReportPdfAsync(vm);

            var safeName = vm.Teacher.FullName.ToLower().Replace(" ", "-");
            var fileName = $"attendance-history-{safeName}-{dateFrom:yyyyMMdd}-{dateTo:yyyyMMdd}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadFormerEmployeeExcel(int id, DateTime? from, DateTime? to)
        {
            var dateFrom = from ?? new DateTime(DateTime.Today.Year, 1, 1);
            var dateTo   = to   ?? DateTime.Today;

            var vm = await _attendanceService.GetTeacherHistoryAsync(id, dateFrom, dateTo);
            var excelBytes = await _reportExportService.ExportIndividualReportExcelAsync(vm);

            var safeName = vm.Teacher.FullName.ToLower().Replace(" ", "-");
            var fileName = $"attendance-history-{safeName}-{dateFrom:yyyyMMdd}-{dateTo:yyyyMMdd}.xlsx";

            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ── Teacher-only: self-service ────────────────────────────────────

        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> MyAttendance(DateTime? from, DateTime? to)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return RedirectToAction("Login", "Account");

            var dateFrom = from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var dateTo   = to   ?? DateTime.Today;

            try
            {
                var vm = await _attendanceService.GetTeacherHistoryAsync(teacherId.Value, dateFrom, dateTo);
                return View(vm);
            }
            catch (KeyNotFoundException)
            {
                // The account behind this session no longer exists
                // (e.g. data was reseeded, or the teacher was removed).
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                TempData["Error"] = "Your account could not be found. Please log in again.";
                return RedirectToAction("Login", "Account");
            }
        }

        // ── Helper ────────────────────────────────────────────────────────

        private int? GetTeacherId()
        {
            var claim = User.FindFirst("TeacherId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}
