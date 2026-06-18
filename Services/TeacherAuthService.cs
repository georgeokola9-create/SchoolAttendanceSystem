using System.Text;
using BiometricAttendanceSystem.Models;
using BiometricAttendanceSystem.Repositories;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BiometricAttendanceSystem.Services
{
    public class TeacherAuthService : ITeacherAuthService
    {
        private readonly ITeacherRepository    _teacherRepo;
        private readonly IConfiguration         _config;
        private readonly ISchoolSettingsService _schoolSettings;

        public TeacherAuthService(
            ITeacherRepository teacherRepo,
            IConfiguration config,
            ISchoolSettingsService schoolSettings)
        {
            _teacherRepo    = teacherRepo;
            _config         = config;
            _schoolSettings = schoolSettings;
        }

        // ── Authentication ───────────────────────────────────────────────

        public async Task<Teacher?> AuthenticateAsync(string username, string password)
        {
            var teacher = await _teacherRepo.GetByUsernameAsync(username);

            if (teacher == null || string.IsNullOrEmpty(teacher.PasswordHash))
                return null;

            return BCrypt.Net.BCrypt.Verify(password, teacher.PasswordHash)
                ? teacher
                : null;
        }

        // ── Set / reset credentials ──────────────────────────────────────

        public async Task<(bool Success, string PlainPassword, string Message)> SetCredentialsAsync(
            int teacherId)
        {
            var teacher = await _teacherRepo.GetByIdAsync(teacherId);
            if (teacher == null)
                return (false, string.Empty, "Teacher not found.");

            var username = teacher.NationalId
                .Replace("/", "")
                .Replace(" ", "")
                .ToUpperInvariant();

            var plain = GeneratePassword();

            teacher.Username             = username;
            teacher.PasswordHash         = BCrypt.Net.BCrypt.HashPassword(plain);
            teacher.CredentialsCreatedAt = DateTime.Now;
            teacher.HasChangedPassword   = false;

            await _teacherRepo.UpdateAsync(teacher);
            return (true, plain, "Credentials set successfully.");
        }

        // ── Change password ──────────────────────────────────────────────

        public async Task<(bool Success, string Message)> ChangePasswordAsync(
            int teacherId, string currentPassword, string newPassword)
        {
            var teacher = await _teacherRepo.GetByIdAsync(teacherId);
            if (teacher == null)
                return (false, "Teacher not found.");

            if (string.IsNullOrEmpty(teacher.PasswordHash) ||
                !BCrypt.Net.BCrypt.Verify(currentPassword, teacher.PasswordHash))
                return (false, "Current password is incorrect.");

            teacher.PasswordHash       = BCrypt.Net.BCrypt.HashPassword(newPassword);
            teacher.HasChangedPassword = true;
            await _teacherRepo.UpdateAsync(teacher);
            return (true, "Password changed.");
        }

        // ── PDF credential slip — single teacher ─────────────────────────

        public async Task<byte[]> GenerateCredentialSlipAsync(int teacherId, string loginUrl)
        {
            var teacher = await _teacherRepo.GetByIdAsync(teacherId);
            if (teacher == null) return Array.Empty<byte>();

            var settings   = await _schoolSettings.GetSettingsAsync();
            var schoolName = settings.SchoolName;

            string plainPassword = "";
            if (loginUrl.Contains("|"))
            {
                var parts     = loginUrl.Split('|', 2);
                loginUrl      = parts[0];
                plainPassword = parts[1];
            }

            return BuildSlipPdf(new[] { (teacher, plainPassword) }, schoolName, loginUrl);
        }

        // ── PDF credential slips — all teachers ──────────────────────────

        public async Task<byte[]> GenerateAllCredentialSlipsAsync(string loginUrl)
        {
            var teachers   = await _teacherRepo.GetAllAsync();
            var settings   = await _schoolSettings.GetSettingsAsync();
            var schoolName = settings.SchoolName;

            var withCreds = teachers
                .Where(t => t.Username != null && t.PasswordHash != null && t.IsActive)
                .Select(t => (t, "See admin to get your password"))
                .ToArray();

            return BuildSlipPdf(withCreds, schoolName, loginUrl);
        }

        // ── PDF builder ──────────────────────────────────────────────────

        private static byte[] BuildSlipPdf(
            IEnumerable<(Teacher Teacher, string PlainPassword)> entries,
            string schoolName,
            string loginUrl)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            return Document.Create(container =>
            {
                foreach (var (teacher, plain) in entries)
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A5);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(11));

                        page.Content().Column(col =>
                        {
                            // Header
                            col.Item()
                                .BorderBottom(2).BorderColor("#1e3a5f")
                                .PaddingBottom(8)        // IContainer — valid here
                                .Column(h =>
                                {
                                    h.Item().Text(schoolName)
                                        .FontSize(16).Bold().FontColor("#1e3a5f").AlignCenter();
                                    h.Item().Text("Staff Login Credentials")
                                        .FontSize(12).FontColor("#475569").AlignCenter();
                                });

                            col.Item().Height(16);

                            // Teacher info box
                            col.Item()
                                .Background("#f8fafc")
                                .Border(1).BorderColor("#e2e8f0")
                                .Padding(14)
                                .Column(info =>
                                {
                                    InfoRow(info, "Name",       teacher.FullName);
                                    InfoRow(info, "National ID", teacher.NationalId);
                                    InfoRow(info, "Department", teacher.Department?.Name ?? "—");
                                });

                            col.Item().Height(16);

                            // Credentials box
                            col.Item()
                                .Background("#eff6ff")
                                .Border(1).BorderColor("#bfdbfe")
                                .Padding(14)
                                .Column(cred =>
                                {
                                    // FIX: use col.Item() wrapper for padding, not on TextBlockDescriptor
                                    cred.Item().PaddingBottom(6).Text("Login Details")
                                        .FontSize(10).Bold().FontColor("#1d4ed8");
                                    LinkRow(cred, "Login URL", loginUrl);
                                    InfoRow(cred, "Username",  teacher.Username ?? "—");
                                    InfoRow(cred, "Password",  string.IsNullOrEmpty(plain) ? "—" : plain);
                                });

                            col.Item().Height(16);

                            // Instructions
                            col.Item()
                                .Background("#fefce8")
                                .Border(1).BorderColor("#fde68a")
                                .Padding(12)
                                .Column(note =>
                                {
                                    note.Item().Text("Instructions")
                                        .Bold().FontSize(10).FontColor("#854d0e");
                                    note.Item().Height(4);
                                    note.Item().Text("1. Connect to school WiFi on your phone.");
                                    note.Item().Text("2. Visit the Login URL above.");
                                    note.Item().Text("3. Log in and go to Register Device.");
                                    note.Item().Text("4. You will be asked to change your password on first login.");
                                    note.Item().Height(6);
                                    note.Item().Text("Keep this slip confidential. Do not share your password.")
                                        .Bold().FontColor("#dc2626");
                                });

                            col.Item().Height(20);

                            // Footer
                            col.Item().AlignRight().Text($"Generated: {DateTime.Now:dd MMM yyyy HH:mm}")
                                .FontSize(8).FontColor("#94a3b8");
                        });
                    });
                }
            }).GeneratePdf();
        }

        private static void InfoRow(ColumnDescriptor col, string label, string value)
        {
            col.Item().PaddingBottom(4).Row(row =>
            {
                row.ConstantItem(90).Text(label + ":").Bold().FontColor("#374151");
                row.RelativeItem().Text(value).FontColor("#0f172a");
            });
        }

        /// <summary>Same as InfoRow but renders the value as a clickable hyperlink.</summary>
        private static void LinkRow(ColumnDescriptor col, string label, string url)
        {
            col.Item().PaddingBottom(4).Row(row =>
            {
                row.ConstantItem(90).Text(label + ":").Bold().FontColor("#374151");
                row.RelativeItem()
                   .Hyperlink(url)
                   .Text(url)
                   .FontColor("#2563eb")
                   .Underline();
            });
        }

        // ── Password generator ───────────────────────────────────────────

        private static string GeneratePassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghjkmnpqrstuvwxyz";
            const string digit = "23456789";

            var rng   = new Random();
            var chars = new StringBuilder();
            chars.Append(upper[rng.Next(upper.Length)]);
            chars.Append(upper[rng.Next(upper.Length)]);
            chars.Append(digit[rng.Next(digit.Length)]);
            chars.Append(digit[rng.Next(digit.Length)]);
            for (int i = 0; i < 4; i++)
                chars.Append(lower[rng.Next(lower.Length)]);

            return new string(chars.ToString().OrderBy(_ => rng.Next()).ToArray());
        }
    }
}
