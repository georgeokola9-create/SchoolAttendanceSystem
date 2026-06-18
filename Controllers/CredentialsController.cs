using System.Net;
using BiometricAttendanceSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiometricAttendanceSystem.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class CredentialsController : Controller
    {
        private readonly ITeacherAuthService _authService;
        private readonly ITeacherService     _teacherService;
        private readonly ILocalNetworkService _localNetwork;

        public CredentialsController(
            ITeacherAuthService authService,
            ITeacherService teacherService,
            ILocalNetworkService localNetwork)
        {
            _authService    = authService;
            _teacherService = teacherService;
            _localNetwork   = localNetwork;
        }

        // ── List all teachers with credential status ──────────────────────

        public async Task<IActionResult> Index()
        {
            var teachers = await _teacherService.GetAllTeachersAsync();
            return View(teachers);
        }

        // ── Create / reset credentials for one teacher ───────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetCredentials(int teacherId)
        {
            var (success, plain, message) =
                await _authService.SetCredentialsAsync(teacherId);

            if (!success)
            {
                TempData["Error"] = message;
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"]       = $"Credentials set. Password: {plain}";
            TempData["LastTeacherId"] = teacherId;
            TempData["LastPlain"]     = plain;
            return RedirectToAction(nameof(Index));
        }

        // ── Download credential slip PDF — single teacher ─────────────────

        public async Task<IActionResult> DownloadSlip(int teacherId)
        {
            var loginUrl  = $"{BuildNetworkBaseUrl()}/Account/Login";
            var plain     = TempData["LastPlain"] as string ?? "";
            var payload   = $"{loginUrl}|{plain}";          // pipe-delimited trick

            var pdf = await _authService.GenerateCredentialSlipAsync(teacherId, payload);
            if (pdf.Length == 0)
                return NotFound();

            var teacher  = await _teacherService.GetTeacherByIdAsync(teacherId);
            var safeName = teacher != null
                ? teacher.FullName.ToLower().Replace(" ", "-")
                : teacherId.ToString();

            return File(pdf, "application/pdf", $"credentials-{safeName}.pdf");
        }

        // ── Download all credential slips as one PDF ──────────────────────

        public async Task<IActionResult> DownloadAll()
        {
            var loginUrl = $"{BuildNetworkBaseUrl()}/Account/Login";
            var pdf      = await _authService.GenerateAllCredentialSlipsAsync(loginUrl);
            return File(pdf, "application/pdf", $"all-credentials-{DateTime.Today:yyyy-MM-dd}.pdf");
        }

        private string BuildNetworkBaseUrl()
        {
            var host = Request.Host;
            var hostName = host.Host;

            var needsNetworkAddress =
                string.Equals(hostName, "localhost", StringComparison.OrdinalIgnoreCase) ||
                IsLoopbackOrAnyAddress(hostName);

            if (!needsNetworkAddress)
                return $"{Request.Scheme}://{host}";

            var localIp = _localNetwork.GetPreferredIPv4Address();
            if (string.IsNullOrWhiteSpace(localIp))
                return $"{Request.Scheme}://{host}";

            var hostWithPort = host.Port.HasValue
                ? $"{localIp}:{host.Port.Value}"
                : localIp;

            return $"{Request.Scheme}://{hostWithPort}";
        }

        private static bool IsLoopbackOrAnyAddress(string hostName)
        {
            if (!IPAddress.TryParse(hostName, out var address))
                return false;

            return IPAddress.IsLoopback(address) ||
                   address.Equals(IPAddress.Any) ||
                   address.Equals(IPAddress.IPv6Any);
        }
    }
}
