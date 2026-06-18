using BiometricAttendanceSystem.Models;
using BiometricAttendanceSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiometricAttendanceSystem.Controllers
{
    [Authorize]
    public class HolidayController : Controller
    {
        private readonly IHolidayService _svc;

        public HolidayController(IHolidayService svc) => _svc = svc;

        public async Task<IActionResult> Index()
        {
            var holidays = await _svc.GetAllHolidaysAsync();
            return View(holidays);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Holiday model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var (success, message) = await _svc.AddHolidayAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                return View(model);
            }

            TempData["Success"] = message;
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var holiday = await _svc.GetHolidayByIdAsync(id);
            if (holiday == null) return NotFound();
            return View(holiday);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Holiday model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var (success, message) = await _svc.UpdateHolidayAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                return View(model);
            }

            TempData["Success"] = message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var holiday = await _svc.GetHolidayByIdAsync(id);
            if (holiday == null) return NotFound();

            await _svc.DeleteHolidayAsync(id);
            TempData["Success"] = $"{holiday.Name} deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
