using BiometricAttendanceSystem.Models;
using BiometricAttendanceSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiometricAttendanceSystem.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly ISchoolSettingsService _svc;

        public SettingsController(ISchoolSettingsService svc) => _svc = svc;

        public async Task<IActionResult> Index()
        {
            var settings = await _svc.GetSettingsAsync();
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SchoolSettings model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var (success, message) = await _svc.UpdateSettingsAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                return View(model);
            }

            TempData["Success"] = message;
            return RedirectToAction(nameof(Index));
        }
    }
}
