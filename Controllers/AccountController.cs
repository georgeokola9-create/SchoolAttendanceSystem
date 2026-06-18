using System.Security.Claims;
using BiometricAttendanceSystem.Models.ViewModels;
using BiometricAttendanceSystem.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiometricAttendanceSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly ITeacherAuthService _teacherAuth;
        private readonly IConfiguration _config;

        public AccountController(ITeacherAuthService teacherAuth, IConfiguration config)
        {
            _teacherAuth = teacherAuth;
            _config = config;
        }

        // ── Shared login page ────────────────────────────────────────────

        [HttpGet]
        public IActionResult Login(string? returnUrl)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectBasedOnRole();

            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVM model, string? returnUrl)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ReturnUrl = returnUrl;
                return View(model);
            }

            // ── Principal check (read-only role) ─────────────────────────
            var principalPassword = _config["Auth:PrincipalPassword"];
            if (!string.IsNullOrWhiteSpace(principalPassword)
                && model.Username.Equals("principal", StringComparison.OrdinalIgnoreCase)
                && model.Password == principalPassword)
            {
                await SignInAsync(
                    name:       "Principal",
                    role:       "Principal",
                    teacherId:  null,
                    rememberMe: model.RememberMe);

                return RedirectToAction("Index", "Attendance");
            }

            // ── Admin check ───────────────────────────────────────────────
            var admins = _config.GetSection("Auth:Admins")
                .GetChildren()
                .Where(admin => !string.IsNullOrWhiteSpace(admin.Value))
                .ToDictionary(admin => admin.Key, admin => admin.Value!, StringComparer.OrdinalIgnoreCase);

            if (admins.TryGetValue(model.Username, out var adminPassword)
                && model.Password == adminPassword)
            {
                await SignInAsync(
                    name:       model.Username,
                    role:       "Administrator",
                    teacherId:  null,
                    rememberMe: model.RememberMe);

                return RedirectToAction("Index", "Attendance");
            }

            // ── Teacher check ─────────────────────────────────────────────
            var teacher = await _teacherAuth.AuthenticateAsync(model.Username, model.Password);
            if (teacher != null)
            {
                await SignInAsync(
                    name:       teacher.FullName,
                    role:       "Teacher",
                    teacherId:  teacher.Id,
                    rememberMe: model.RememberMe);

                // First login — force password change
                if (!teacher.HasChangedPassword)
                {
                    TempData["TempPassword"] = model.Password;
                    return RedirectToAction(nameof(ChangePassword));
                }

                return RedirectToAction("MyAttendance", "Teacher");
            }

            // ── Neither matched ───────────────────────────────────────────
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.Error = "Incorrect username or password.";
            return View(model);
        }

        // ── Change password (teacher first-login) ────────────────────────

        [HttpGet]
        [Authorize(Roles = "Teacher")]
        public IActionResult ChangePassword() => View(new ChangePasswordVM());

        [HttpPost]
        [Authorize(Roles = "Teacher")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var teacherId = GetTeacherId();
            if (teacherId == null)
                return RedirectToAction(nameof(Login));

            var (success, message) = await _teacherAuth.ChangePasswordAsync(
                teacherId.Value, model.CurrentPassword, model.NewPassword);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                return View(model);
            }

            TempData["Success"] = "Password changed successfully.";
            return RedirectToAction("MyAttendance", "Teacher");
        }

        // ── Logout ───────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private async Task SignInAsync(string name, string role, int? teacherId, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, name),
                new(ClaimTypes.Role, role)
            };

            if (teacherId.HasValue)
                claims.Add(new Claim("TeacherId", teacherId.Value.ToString()));

            var identity   = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal  = new ClaimsPrincipal(identity);
            var properties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc   = rememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                properties);
        }

        private IActionResult RedirectBasedOnRole()
        {
            if (User.IsInRole("Administrator"))
                return RedirectToAction("Index", "Attendance");
            if (User.IsInRole("Principal"))
                return RedirectToAction("Index", "Attendance");
            if (User.IsInRole("Teacher"))
                return RedirectToAction("MyAttendance", "Teacher");
            return RedirectToAction(nameof(Login));
        }

        private int? GetTeacherId()
        {
            var claim = User.FindFirst("TeacherId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}
