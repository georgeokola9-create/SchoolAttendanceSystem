using BiometricAttendanceSystem.Hubs;
using BiometricAttendanceSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace BiometricAttendanceSystem.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class DeviceRequestsController : Controller
    {
        private readonly IDeviceReregistrationService  _svc;
        private readonly IHubContext<NotificationsHub> _hub;

        public DeviceRequestsController(
            IDeviceReregistrationService svc,
            IHubContext<NotificationsHub> hub)
        {
            _svc = svc;
            _hub = hub;
        }

        public async Task<IActionResult> Index()
        {
            var requests = await _svc.GetPendingRequestsAsync();
            return View(requests);
        }

        [HttpGet]
        public async Task<IActionResult> History()
        {
            var all = await _svc.GetAllAsync();
            return View(all);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadSummaryPdf()
        {
            var all = await _svc.GetAllAsync();
            var bytes = _svc.GenerateSummaryPdf(all);
            return File(bytes, "application/pdf", $"Device-Rereg-Summary-{DateTime.Now:yyyyMMdd}.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> DownloadAuditPdf(string? teacher, string? status)
        {
            var all = await _svc.GetAllAsync();

            // Apply same filters as the client-side JS
            if (!string.IsNullOrWhiteSpace(teacher))
                all = all.Where(r => r.Teacher?.FullName
                    .Contains(teacher, StringComparison.OrdinalIgnoreCase) == true).ToList();

            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<BiometricAttendanceSystem.Models.ReregistrationStatus>(status, true, out var parsedStatus))
                all = all.Where(r => r.Status == parsedStatus).ToList();

            var bytes = _svc.GenerateAuditPdf(all);
            var label = (!string.IsNullOrWhiteSpace(teacher) || !string.IsNullOrWhiteSpace(status))
                ? "Filtered"
                : "Full";
            return File(bytes, "application/pdf", $"Device-Rereg-AuditLog-{label}-{DateTime.Now:yyyyMMdd}.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> PendingCount()
        {
            var count = await _svc.GetPendingCountAsync();
            return Json(new { count });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int requestId)
        {
            var ok = await _svc.ApproveAsync(requestId);
            TempData[ok ? "Success" : "Error"] = ok
                ? "Device swap approved. Teacher can now check in from their new device."
                : "Request not found or already reviewed.";

            await NotifyAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int requestId, string? note)
        {
            var ok = await _svc.RejectAsync(requestId, note ?? "");
            TempData[ok ? "Success" : "Error"] = ok
                ? "Request rejected. Teacher's device remains unchanged."
                : "Request not found or already reviewed.";

            await NotifyAsync();
            return RedirectToAction(nameof(Index));
        }

        private async Task NotifyAsync()
        {
            var pendingCount = await _svc.GetPendingCountAsync();
            await _hub.Clients.All.SendAsync("PendingCountUpdated", pendingCount);
            await _hub.Clients.All.SendAsync("RequestListUpdated");
        }
    }
}
