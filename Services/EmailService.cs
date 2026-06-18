using System.Net;
using System.Net.Mail;

namespace BiometricAttendanceSystem.Services
{
    public interface IEmailService
    {
        Task<(bool Success, string Message)> SendCredentialSlipAsync(
            string toEmail,
            string teacherName,
            byte[] pdfBytes,
            string fileName,
            string? schoolName = null);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task<(bool Success, string Message)> SendCredentialSlipAsync(
            string toEmail,
            string teacherName,
            byte[] pdfBytes,
            string fileName,
            string? schoolName = null)
        {
            try
            {
                var host       = _config["Email:SmtpHost"]     ?? "smtp.gmail.com";
                var port       = int.Parse(_config["Email:SmtpPort"] ?? "587");
                var sender     = _config["Email:SenderEmail"]  ?? "";
                var senderName = _config["Email:SenderName"]   ?? "Attendance System";
                var password   = _config["Email:AppPassword"]  ?? "";
                schoolName     = schoolName ?? _config["School:Name"] ?? "School Attendance System";

                using var client = new SmtpClient(host, port)
                {
                    Credentials    = new NetworkCredential(sender, password),
                    EnableSsl      = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

                using var mail = new MailMessage
                {
                    From       = new MailAddress(sender, senderName),
                    Subject    = $"{schoolName} — Your Login Credentials",
                    IsBodyHtml = true,
                    Body       = BuildEmailBody(teacherName, schoolName)
                };

                mail.To.Add(toEmail);

                // Attach PDF
                var stream = new MemoryStream(pdfBytes);
                mail.Attachments.Add(new Attachment(stream, fileName, "application/pdf"));

                await client.SendMailAsync(mail);
                return (true, "Credential slip emailed successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Email failed: {ex.Message}");
            }
        }

        private static string BuildEmailBody(string teacherName, string schoolName) => $"""
            <div style="font-family:Arial,sans-serif;max-width:520px;margin:0 auto">
                <div style="background:#1e3a5f;padding:24px 28px;border-radius:10px 10px 0 0">
                    <h2 style="color:#fff;margin:0;font-size:20px">{schoolName}</h2>
                    <p style="color:#93c5fd;margin:4px 0 0;font-size:13px">Staff Attendance System</p>
                </div>
                <div style="background:#f8fafc;padding:28px;border:1px solid #e2e8f0;border-top:none;border-radius:0 0 10px 10px">
                    <p style="color:#0f172a;font-size:15px">Dear <strong>{teacherName}</strong>,</p>
                    <p style="color:#475569;font-size:14px;line-height:1.6">
                        Your login credentials for the staff attendance system have been set up.
                        Please find your credential slip attached to this email as a PDF.
                    </p>
                    <div style="background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px;padding:16px;margin:20px 0">
                        <p style="color:#1d4ed8;font-weight:700;margin:0 0 6px;font-size:13px">📎 ATTACHED: Your Credential Slip</p>
                        <p style="color:#475569;font-size:13px;margin:0">Open the PDF to find your username, password, and login URL.</p>
                    </div>
                    <p style="color:#475569;font-size:13px;line-height:1.6">
                        <strong>Important:</strong> You will be required to change your password on first login.
                        Keep your credentials confidential and do not share them with anyone.
                    </p>
                    <p style="color:#94a3b8;font-size:12px;margin-top:24px;border-top:1px solid #e2e8f0;padding-top:16px">
                        This is an automated message from {schoolName}. Do not reply to this email.
                    </p>
                </div>
            </div>
            """;
    }
}
