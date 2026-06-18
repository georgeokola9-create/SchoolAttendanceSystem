using BiometricAttendanceSystem.Models;
using BiometricAttendanceSystem.Models.ViewModels;

namespace BiometricAttendanceSystem.Services
{
    public interface ILocalNetworkService
    {
        string? GetPreferredIPv4Address();
        string? GetPreferredSubnetPrefix();
    }

    public interface ITeacherService
    {
        Task<List<Teacher>>                      GetAllTeachersAsync();
        Task<Teacher?> GetTeacherByDeviceCookieAsync(string deviceCookie);
        Task<List<Department>> GetDepartmentsAsync();
        Task<Teacher?>                           GetTeacherByIdAsync(int id);
        Task<Teacher?>                           GetTeacherByIdIncludingInactiveAsync(int id);
        Task<List<Teacher>>                      GetFormerEmployeesAsync();
        Task<(bool Success, string Message)>     RegisterTeacherAsync(Teacher teacher);
        Task<(bool Success, string Message)>     UpdateTeacherAsync(Teacher teacher);
        Task<bool>                               DeactivateTeacherAsync(int id);
        Task<TeacherSearchVM>                    SearchTeachersAsync(string query, string searchBy);
    }

    public interface ITeacherAuthService
    {
        /// <summary>Validate username + password. Returns the teacher on success.</summary>
        Task<Teacher?> AuthenticateAsync(string username, string password);

        /// <summary>Create or reset credentials for a teacher. Returns the plain-text password (shown once).</summary>
        Task<(bool Success, string PlainPassword, string Message)> SetCredentialsAsync(int teacherId);

        /// <summary>Change a teacher's own password.</summary>
        Task<(bool Success, string Message)> ChangePasswordAsync(int teacherId, string currentPassword, string newPassword);

        /// <summary>Generate a printable PDF credential slip for one teacher.</summary>
        Task<byte[]> GenerateCredentialSlipAsync(int teacherId, string loginUrl);

        /// <summary>Generate a single PDF with all teacher credential slips (one per page).</summary>
        Task<byte[]> GenerateAllCredentialSlipsAsync(string loginUrl);
    }

    // ── QR check-in ───────────────────────────────────────────────────────
    public interface IDeviceReregistrationService
    {
        Task<DeviceReregistrationRequest> RequestReregistrationAsync(int teacherId, string requestIp);
        Task<List<DeviceReregistrationRequest>> GetPendingRequestsAsync();
        Task<List<DeviceReregistrationRequest>> GetAllAsync();
        Task<int> GetPendingCountAsync();
        Task<bool> ApproveAsync(int requestId);
        Task<bool> RejectAsync(int requestId, string note);
        byte[] GenerateSummaryPdf(List<DeviceReregistrationRequest> requests);
        byte[] GenerateAuditPdf(List<DeviceReregistrationRequest> requests);
    }

    public interface IQrCheckInService
    {
        /// <summary>Generate the current QR payload URL — static until reset.</summary>
        string GenerateQrUrl(string baseUrl);

        /// <summary>Validate a QR token.</summary>
        bool ValidateToken(string token);

        /// <summary>Reset the static token — invalidates all printed QR codes.</summary>
        string ResetToken();

        /// <summary>
        /// Record a QR check-in for the given teacher.
        /// Verifies: token validity, WiFi subnet, registered device cookie.
        /// </summary>
        Task<QrCheckInResultVM> CheckInAsync(
            int teacherId,
            string token,
            string requestIp,
            string? deviceCookie);

        /// <summary>Register (or re-register) a device for a teacher.</summary>
        Task<(bool Success, string DeviceId, string Message)> RegisterDeviceAsync(
            int teacherId,
            string requestIp);
    }

    public interface IAttendanceService
    {
        Task<AttendanceDashboardVM>              GetDashboardAsync(DateTime? date = null);
        Task<(bool Success, string Message)>     RecordCheckInAsync(int teacherId);
        Task<(bool Success, string Message)>     RecordCheckInByNationalIdAsync(string nationalId);
        Task<int>                                MarkAbsenteesAsync(DateTime date);
        Task<TeacherHistoryVM>                   GetTeacherHistoryAsync(int id, DateTime from, DateTime to);
    }

    public interface IReportService
    {
        Task<DailyReportVM>      GetDailyReportAsync(DateTime date);
        Task<WeeklyReportVM>     GetWeeklyReportAsync(int weekYear, int weekNumber);
        Task<MonthlyReportVM>    GetMonthlyReportAsync(int month, int year);
        Task<DepartmentReportVM> GetDepartmentReportAsync(int deptId, DateTime from, DateTime to);
        Task<TeacherHistoryVM>   GetIndividualReportAsync(int teacherId, DateTime from, DateTime to);
    }

    public interface IChartService
    {
        Task<ChartResultVM> GenerateChartsAsync(ChartDataVM chartData);
    }



}
