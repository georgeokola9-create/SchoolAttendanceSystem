using System.Security.Cryptography;
using System.Text;
using System.Net;
using BiometricAttendanceSystem.Models;
using BiometricAttendanceSystem.Models.ViewModels;
using BiometricAttendanceSystem.Repositories;

namespace BiometricAttendanceSystem.Services
{
    public class QrCheckInService : IQrCheckInService
    {
        private const int TokenWindowSeconds = 60;

        private readonly ITeacherRepository             _teacherRepo;
        private readonly IAttendanceRepository           _attendanceRepo;
        private readonly IConfiguration                  _config;
        private readonly ILocalNetworkService            _localNetwork;
        private readonly IDeviceReregistrationService?   _reregSvc;

        private TimeSpan LateThreshold => TimeSpan.Parse(_config["CheckIn:LateAfter"] ?? "08:30");
        private TimeSpan CutOffTime    => TimeSpan.Parse(_config["CheckIn:CutOffAt"]  ?? "17:00");

        public QrCheckInService(
            ITeacherRepository teacherRepo,
            IAttendanceRepository attendanceRepo,
            IConfiguration config,
            ILocalNetworkService localNetwork,
            IDeviceReregistrationService? reregSvc = null)
        {
            _teacherRepo    = teacherRepo;
            _attendanceRepo = attendanceRepo;
            _config         = config;
            _localNetwork   = localNetwork;
            _reregSvc       = reregSvc;
        }

        // ── Token generation ─────────────────────────────────────────────

        public string GenerateQrUrl(string baseUrl)
        {
            var token = GetStaticToken();
            return $"{baseUrl.TrimEnd('/')}/Qr/CheckIn?token={Uri.EscapeDataString(token)}";
        }

        public bool ValidateToken(string token)
        {
            // Static token — just compare directly
            return string.Equals(token, GetStaticToken(), StringComparison.Ordinal);
        }

        public string ResetToken()
        {
            // Generate a new static token and persist it
            var newToken = GenerateNewStaticToken();
            _config["QrToken:StaticToken"] = newToken;
            return newToken;
        }

        private string GetStaticToken()
        {
            // Use persisted static token if available, otherwise generate one
            var existing = _config["QrToken:StaticToken"];
            if (!string.IsNullOrWhiteSpace(existing))
                return existing;

            // First run — generate and store
            var token = GenerateNewStaticToken();
            _config["QrToken:StaticToken"] = token;
            return token;
        }

        private string GenerateNewStaticToken()
        {
            var secret = _config["QrToken:Secret"] ?? "default-secret-change-me";
            var data   = Encoding.UTF8.GetBytes($"static:{Guid.NewGuid()}");
            var key    = Encoding.UTF8.GetBytes(secret);
            using var hmac = new HMACSHA256(key);
            return Convert.ToBase64String(hmac.ComputeHash(data))[..16];
        }

        // ── Check-in ─────────────────────────────────────────────────────

        public async Task<QrCheckInResultVM> CheckInAsync(
            int teacherId,
            string token,
            string requestIp,
            string? deviceCookie)
        {
            // 1. Token validity
            if (!ValidateToken(token))
                return Fail("InvalidToken", "QR code has expired. Scan the latest code.");

            // 2. WiFi subnet check
            if (!IsAllowedNetwork(requestIp, out var expectedSubnet))
                return Fail("WifiBlocked",
                    $"Check-in requires the same WiFi as the attendance computer. Your IP ({requestIp}) is not on the allowed network ({expectedSubnet}).");

            // 3. Load teacher
            var teacher = await _teacherRepo.GetByIdAsync(teacherId);
            if (teacher == null || !teacher.IsActive)
                return Fail("NotFound", "Teacher account not found or inactive.");

            // 4. Device check
            if (string.IsNullOrEmpty(teacher.RegisteredDeviceId))
                return Fail("UnknownDevice",
                    "No device is registered for your account. Go to Register Device first.");

            if (deviceCookie != teacher.RegisteredDeviceId)
                return Fail("UnknownDevice",
                    "This device is not registered to your account.");

            // 5. Already checked in today?
            var today    = DateTime.Today;
            var existing = await _attendanceRepo.GetByTeacherAndDateAsync(teacherId, today);

            if (existing != null && existing.CheckInTime.HasValue)
                return Fail("AlreadyCheckedIn",
                    $"You already checked in today at {existing.CheckInDisplay}.");

            // 6. Cut-off check
            var timeOfDay = DateTime.Now.TimeOfDay;
            if (timeOfDay > CutOffTime)
                return Fail("InvalidToken", "Check-in window has closed for today.");

            // 7. Determine status
            var status = timeOfDay <= LateThreshold
                ? AttendanceStatus.Present
                : AttendanceStatus.Late;

            // 8. Record
            var now = DateTime.Now;
            if (existing == null)
            {
                await _attendanceRepo.AddAsync(new AttendanceRecord
                {
                    TeacherId   = teacherId,
                    Date        = today,
                    CheckInTime = now.TimeOfDay,
                    Status      = status,
                    Notes       = "QR check-in"
                });
            }
            else
            {
                existing.CheckInTime = now.TimeOfDay;
                existing.Status      = status;
                existing.Notes       = "QR check-in";
                await _attendanceRepo.UpdateAsync(existing);
            }

            return new QrCheckInResultVM
            {
                Success     = true,
                Message     = status == AttendanceStatus.Present
                              ? "Check-in recorded. Good morning!"
                              : "Late check-in recorded.",
                Status      = status.ToString(),
                CheckInTime = now.ToString("hh:mm tt"),
                TeacherName = teacher.FullName
            };
        }

        // ── Device registration ──────────────────────────────────────────

        public async Task<(bool Success, string DeviceId, string Message)> RegisterDeviceAsync(
            int teacherId, string requestIp)
        {
            var teacher = await _teacherRepo.GetByIdAsync(teacherId);
            if (teacher == null)
                return (false, string.Empty, "Teacher not found.");

            // First-time registration — no existing device, approve instantly
            if (string.IsNullOrEmpty(teacher.RegisteredDeviceId))
            {
                var deviceId = Guid.NewGuid().ToString("N");
                teacher.RegisteredDeviceId = deviceId;
                teacher.DeviceRegisteredAt = DateTime.Now;
                teacher.DeviceRegisteredIp = requestIp;
                await _teacherRepo.UpdateAsync(teacher);
                return (true, deviceId, "Device registered successfully.");
            }

            // Re-registration — requires admin approval
            return (false, string.Empty, "pending_approval");
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private bool IsAllowedNetwork(string requestIp, out string expectedSubnet)
        {
            var clientIp = NormalizeIp(requestIp);
            var configuredSubnets = (_config["CheckIn:AllowedWifiSubnet"] ?? "")
                .Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var allowedSubnets = configuredSubnets.Length > 0
                ? configuredSubnets
                : new[] { _localNetwork.GetPreferredSubnetPrefix() ?? "" };

            expectedSubnet = string.Join(", ", allowedSubnets.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (string.IsNullOrWhiteSpace(expectedSubnet))
                expectedSubnet = "not detected";

            if (IPAddress.TryParse(clientIp, out var parsedIp) && IPAddress.IsLoopback(parsedIp))
                return true;

            return allowedSubnets.Any(subnet =>
                !string.IsNullOrWhiteSpace(subnet) &&
                clientIp.StartsWith(subnet, StringComparison.Ordinal));
        }

        private static string NormalizeIp(string requestIp)
        {
            if (!IPAddress.TryParse(requestIp, out var address))
                return requestIp;

            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();

            return address.ToString();
        }

        private static QrCheckInResultVM Fail(string reason, string message) =>
            new() { Success = false, FailureReason = reason, Message = message };
    }
}
