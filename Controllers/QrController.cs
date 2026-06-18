using System.Net;
using BiometricAttendanceSystem.Models.ViewModels;
using BiometricAttendanceSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using BiometricAttendanceSystem.Hubs;

namespace BiometricAttendanceSystem.Controllers
{
    public class QrController : Controller
    {
        private readonly IQrCheckInService              _qrService;
        private readonly ILocalNetworkService           _localNetwork;
        private readonly ITeacherService                _teacherService;
        private readonly IDeviceReregistrationService   _reregSvc;
        private readonly IHubContext<NotificationsHub>  _hub;
        private readonly ISchoolSettingsService         _schoolSettings;

        private const string DeviceCookieName = "BAS_DeviceId";

        public QrController(
            IQrCheckInService qrService,
            ILocalNetworkService localNetwork,
            ITeacherService teacherService,
            IDeviceReregistrationService reregSvc,
            IHubContext<NotificationsHub> hub,
            ISchoolSettingsService schoolSettings)
        {
            _qrService      = qrService;
            _localNetwork   = localNetwork;
            _teacherService = teacherService;
            _reregSvc       = reregSvc;
            _hub            = hub;
            _schoolSettings = schoolSettings;
        }

        // ── Admin: fullscreen QR display ─────────────────────────────────

        [AllowAnonymous]
        public async Task<IActionResult> Display()
        {
            var baseUrl            = BuildQrBaseUrl();
            ViewBag.QrUrl          = _qrService.GenerateQrUrl(baseUrl);
            ViewBag.BaseUrl        = baseUrl;
            ViewBag.RefreshSeconds = 55;
            var settings           = await _schoolSettings.GetSettingsAsync();
            ViewBag.SchoolName     = settings?.SchoolName ?? "KP Attendance";
            return View();
        }

        [AllowAnonymous]
        public IActionResult CurrentToken()
        {
            var baseUrl = BuildQrBaseUrl();
            return Content(_qrService.GenerateQrUrl(baseUrl));
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResetQrToken()
        {
            _qrService.ResetToken();
            TempData["Success"] = "QR code has been reset. All previously printed QR codes are now invalid.";
            return RedirectToAction(nameof(Display));
        }

        // ── Teacher: scan landing page ────────────────────────────────────

        [AllowAnonymous]
        public async Task<IActionResult> CheckIn(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Display");

            var ip           = GetClientIp();
            var deviceCookie = Request.Cookies[DeviceCookieName];

            // 1. Device cookie path — seamless check-in, no login required
            if (!string.IsNullOrEmpty(deviceCookie))
            {
                var teacher = await _teacherService.GetTeacherByDeviceCookieAsync(deviceCookie);
                if (teacher != null)
                {
                    var result = await _qrService.CheckInAsync(teacher.Id, token, ip, deviceCookie);
                    if (result.Success)
                        await NotifyDashboardAsync();
                    return View("CheckInResult", result);
                }

                // Cookie exists but didn't match any teacher — may be stale after re-registration approval.
                // If teacher is logged in, refresh their cookie to the newly approved device ID.
                if (User.Identity?.IsAuthenticated == true && User.IsInRole("Teacher"))
                {
                    var teacherId = GetTeacherId();
                    if (teacherId != null)
                    {
                        var teacher2 = await _teacherService.GetTeacherByIdAsync(teacherId.Value);
                        if (teacher2 != null && !string.IsNullOrEmpty(teacher2.RegisteredDeviceId))
                        {
                            // Refresh the cookie to the approved device ID
                            Response.Cookies.Append(DeviceCookieName, teacher2.RegisteredDeviceId, new CookieOptions
                            {
                                HttpOnly = true,
                                Secure   = false,
                                SameSite = SameSiteMode.Lax,
                                Expires  = DateTimeOffset.UtcNow.AddYears(10)
                            });

                            var result = await _qrService.CheckInAsync(teacherId.Value, token, ip, teacher2.RegisteredDeviceId);
                            if (result.Success)
                                await NotifyDashboardAsync();
                            return View("CheckInResult", result);
                        }
                    }
                }
            }

            // 2. Logged-in session path
            if (User.Identity?.IsAuthenticated == true && User.IsInRole("Teacher"))
            {
                var teacherId = GetTeacherId();
                if (teacherId != null)
                {
                    var teacher = await _teacherService.GetTeacherByIdAsync(teacherId.Value);

                    // If teacher has a registered device but cookie is missing/stale, refresh it
                    if (teacher != null && !string.IsNullOrEmpty(teacher.RegisteredDeviceId)
                        && deviceCookie != teacher.RegisteredDeviceId)
                    {
                        Response.Cookies.Append(DeviceCookieName, teacher.RegisteredDeviceId, new CookieOptions
                        {
                            HttpOnly = true,
                            Secure   = false,
                            SameSite = SameSiteMode.Lax,
                            Expires  = DateTimeOffset.UtcNow.AddYears(10)
                        });
                        deviceCookie = teacher.RegisteredDeviceId;
                    }

                    var result = await _qrService.CheckInAsync(teacherId.Value, token, ip, deviceCookie);
                    if (result.Success)
                        await NotifyDashboardAsync();
                    return View("CheckInResult", result);
                }
            }

            // 3. Unknown device — show helpful page rather than a login wall
            return View("RegisterDevicePrompt", new RegisterDevicePromptVM
            {
                Token   = token,
                Message = "Your device is not registered. Please log in once to register this device, then future scans will check you in automatically."
            });
        }

        // ── Device registration ──────────────────────────────────────────

        [Authorize(Roles = "Teacher")]
        public IActionResult RegisterDevice()
        {
            ViewBag.TeacherId = GetTeacherId();
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Teacher")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterDevice(string confirm)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null)
                return RedirectToAction("Login", "Account");

            var ip = GetClientIp();
            var (success, deviceId, message) = await _qrService.RegisterDeviceAsync(teacherId.Value, ip);

            if (!success)
            {
                if (message == "pending_approval")
                {
                    // Create the actual reregistration request row
                    await _reregSvc.RequestReregistrationAsync(teacherId.Value, ip);

                    // Notify all connected admin browsers in real time
                    var pendingCount = await _reregSvc.GetPendingCountAsync();
                    await _hub.Clients.All.SendAsync("PendingCountUpdated", pendingCount);
                    await _hub.Clients.All.SendAsync("RequestListUpdated");
                }

                ViewBag.Error     = message;
                ViewBag.TeacherId = teacherId;
                return View();
            }

            Response.Cookies.Append(DeviceCookieName, deviceId, new CookieOptions
            {
                HttpOnly = true,
                Secure   = false,   // allow http for local network
                SameSite = SameSiteMode.Lax,
                Expires  = DateTimeOffset.UtcNow.AddYears(10)
            });

            ViewBag.Success = true;
            return View();
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private async Task NotifyDashboardAsync()
        {
            await _hub.Clients.All.SendAsync("AttendanceUpdated");
        }

        private string BuildQrBaseUrl()
        {
            var host     = Request.Host;
            var hostName = host.Host;

            var needsNetworkAddress =
                string.Equals(hostName, "localhost", StringComparison.OrdinalIgnoreCase) ||
                IsLoopbackOrAnyAddress(hostName);

            if (!needsNetworkAddress)
                return $"{Request.Scheme}://{host}";

            // Use the PC's actual LAN IP address instead of a .local mDNS hostname.
            // mDNS resolution is unreliable on many Android devices/browsers, causing
            // DNS_PROBE_FINISHED_NXDOMAIN errors when scanning the QR code. A direct
            // IP address requires no name resolution step at all, so it works
            // consistently across every device.
            //
            // This uses the SAME interface-selection logic that QrCheckInService uses
            // for its WiFi-subnet check (both go through LocalNetworkService's
            // GetPreferredAddress()), so the QR code's embedded IP and the subnet the
            // check-in validation expects always agree with each other.
            var lanIp = _localNetwork.GetPreferredIPv4Address();

            if (string.IsNullOrWhiteSpace(lanIp))
            {
                // Fallback: no usable LAN interface was found (e.g. running in a
                // container, or no network adapters are up). Fall back to the
                // previous .local behavior rather than returning an empty host.
                var mdnsHost = $"{Environment.MachineName.ToLowerInvariant()}.local";
                var fallbackHost = host.Port.HasValue
                    ? $"{mdnsHost}:{host.Port.Value}"
                    : mdnsHost;

                return $"{Request.Scheme}://{fallbackHost}";
            }

            var hostWithPort = host.Port.HasValue
                ? $"{lanIp}:{host.Port.Value}"
                : lanIp;

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

        private int? GetTeacherId()
        {
            var claim = User.FindFirst("TeacherId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        private string GetClientIp()
        {
            var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwarded))
                return forwarded.Split(',')[0].Trim();
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
