using BiometricAttendanceSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BiometricAttendanceSystem.Filters
{
    /// <summary>
    /// Runs on every controller action and injects the school name into ViewBag
    /// so all views can use @ViewBag.SchoolName without any extra code.
    /// </summary>
    public class SchoolSettingsFilter : IAsyncActionFilter
    {
        private readonly ISchoolSettingsService _settings;

        public SchoolSettingsFilter(ISchoolSettingsService settings)
        {
            _settings = settings;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.Controller is Controller controller)
            {
                try
                {
                    var settings = await _settings.GetSettingsAsync();
                    controller.ViewBag.SchoolName = settings.SchoolName;
                }
                catch
                {
                    controller.ViewBag.SchoolName = "Attendance System";
                }
            }

            await next();
        }
    }
}
