using BiometricAttendanceSystem.Data;
using BiometricAttendanceSystem.Filters;
using BiometricAttendanceSystem.Hubs;
using BiometricAttendanceSystem.Repositories;
using BiometricAttendanceSystem.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<SchoolSettingsFilter>();
});
builder.Services.AddSignalR();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath        = "/Account/Login";
        options.LogoutPath       = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan   = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();

// ── Repositories ─────────────────────────────────────────────────────────
builder.Services.AddScoped<ITeacherRepository,        TeacherRepository>();
builder.Services.AddScoped<IAttendanceRepository,     AttendanceRepository>();
builder.Services.AddScoped<IDepartmentRepository,     DepartmentRepository>();
builder.Services.AddScoped<IHolidayRepository,        HolidayRepository>();
builder.Services.AddScoped<ISchoolSettingsRepository, SchoolSettingsRepository>();

// ── Services ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<ITeacherService,       TeacherService>();
builder.Services.AddScoped<IAttendanceService,    AttendanceService>();
builder.Services.AddScoped<IReportService,        ReportService>();
builder.Services.AddScoped<IReportExportService,  ReportExportService>();
builder.Services.AddScoped<IChartService,         ChartService>();
builder.Services.AddScoped<IHolidayService,       HolidayService>();
builder.Services.AddScoped<ISchoolSettingsService, SchoolSettingsService>();
builder.Services.AddScoped<IDepartmentService,    DepartmentService>();
builder.Services.AddScoped<ILocalNetworkService,  LocalNetworkService>();

// ── New: teacher auth + QR check-in ──────────────────────────────────────
builder.Services.AddScoped<ITeacherAuthService,              TeacherAuthService>();
builder.Services.AddScoped<IQrCheckInService,                QrCheckInService>();
builder.Services.AddScoped<IDeviceReregistrationService,     DeviceReregistrationService>();
builder.Services.AddScoped<IEmailService,                    EmailService>();

// ── ─────────────────────────────────────────────────────────────────────

var app = builder.Build();

// Ensure the SQLite database and schema exist, in every environment
// (this runs in Production too, since a Windows service defaults to Production).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapHub<NotificationsHub>("/hubs/notifications");

if (args.Contains("--seed-demo"))
{
    await DemoDataSeeder.RunAsync(app.Services);
    return;
}

app.Run();
