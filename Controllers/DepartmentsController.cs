using BiometricAttendanceSystem.Models;
using BiometricAttendanceSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiometricAttendanceSystem.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class DepartmentsController : Controller
    {
        private readonly IDepartmentService _departmentService;

        public DepartmentsController(IDepartmentService departmentService)
            => _departmentService = departmentService;

        public async Task<IActionResult> Index()
        {
            var departments = await _departmentService.GetAllAsync();
            return View(departments);
        }

        public IActionResult Create() => View(new Department());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Department department)
        {
            if (!ModelState.IsValid)
                return View(department);

            var (success, message) = await _departmentService.CreateAsync(department);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                return View(department);
            }

            TempData["Success"] = message;
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var department = await _departmentService.GetByIdAsync(id);
            if (department == null) return NotFound();
            return View(department);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Department department)
        {
            if (id != department.Id) return BadRequest();

            if (!ModelState.IsValid)
                return View(department);

            var (success, message) = await _departmentService.UpdateAsync(department);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                return View(department);
            }

            TempData["Success"] = message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var (success, message) = await _departmentService.DeleteAsync(id);
            TempData[success ? "Success" : "Error"] = message;
            return RedirectToAction(nameof(Index));
        }
    }
}
