using BiometricAttendanceSystem.Data;
using BiometricAttendanceSystem.Models;
using BiometricAttendanceSystem.Repositories;
using Microsoft.EntityFrameworkCore;
using iText.Kernel.Pdf;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Borders;
using iText.Layout.Properties;

namespace BiometricAttendanceSystem.Services
{
    public class DeviceReregistrationService : IDeviceReregistrationService
    {
        private readonly AppDbContext      _db;
        private readonly ITeacherRepository _teacherRepo;

        public DeviceReregistrationService(AppDbContext db, ITeacherRepository teacherRepo)
        {
            _db          = db;
            _teacherRepo = teacherRepo;
        }

        // Teacher submits a re-registration request — does NOT change device yet
        public async Task<DeviceReregistrationRequest> RequestReregistrationAsync(
            int teacherId, string requestIp)
        {
            // Cancel any existing pending request for this teacher
            var existing = await _db.DeviceReregistrationRequests
                .Where(r => r.TeacherId == teacherId && r.Status == ReregistrationStatus.Pending)
                .ToListAsync();

            _db.DeviceReregistrationRequests.RemoveRange(existing);

            var request = new DeviceReregistrationRequest
            {
                TeacherId       = teacherId,
                RequestedIp     = requestIp,
                RequestedAt     = DateTime.Now,
                Status          = ReregistrationStatus.Pending,
                PendingDeviceId = Guid.NewGuid().ToString("N")
            };

            _db.DeviceReregistrationRequests.Add(request);
            await _db.SaveChangesAsync();
            return request;
        }

        public async Task<List<DeviceReregistrationRequest>> GetPendingRequestsAsync()
        {
            return await _db.DeviceReregistrationRequests
                .Include(r => r.Teacher)
                    .ThenInclude(t => t!.Department)
                .Where(r => r.Status == ReregistrationStatus.Pending)
                .OrderBy(r => r.RequestedAt)
                .ToListAsync();
        }

        public async Task<List<DeviceReregistrationRequest>> GetAllAsync()
        {
            return await _db.DeviceReregistrationRequests
                .Include(r => r.Teacher)
                    .ThenInclude(t => t!.Department)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();
        }

        public async Task<int> GetPendingCountAsync()
        {
            return await _db.DeviceReregistrationRequests
                .CountAsync(r => r.Status == ReregistrationStatus.Pending);
        }

        // Admin approves — NOW the device is swapped
        public async Task<bool> ApproveAsync(int requestId)
        {
            var request = await _db.DeviceReregistrationRequests
                .Include(r => r.Teacher)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null || request.Status != ReregistrationStatus.Pending)
                return false;

            var teacher = request.Teacher;
            if (teacher == null) return false;

            teacher.RegisteredDeviceId = request.PendingDeviceId;
            teacher.DeviceRegisteredAt = DateTime.Now;
            teacher.DeviceRegisteredIp = request.RequestedIp;

            request.Status     = ReregistrationStatus.Approved;
            request.ReviewedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            return true;
        }

        // Admin rejects — request closed, device unchanged
        public async Task<bool> RejectAsync(int requestId, string note)
        {
            var request = await _db.DeviceReregistrationRequests
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null || request.Status != ReregistrationStatus.Pending)
                return false;

            request.Status     = ReregistrationStatus.Rejected;
            request.ReviewedAt = DateTime.Now;
            request.ReviewNote = note;

            await _db.SaveChangesAsync();
            return true;
        }

        // ── PDF generation ────────────────────────────────────────────────

        public byte[] GenerateSummaryPdf(List<DeviceReregistrationRequest> requests)
        {
            var byTeacher = requests
                .GroupBy(r => r.TeacherId)
                .Select(g => new
                {
                    Name       = g.First().Teacher?.FullName ?? "Unknown",
                    Department = g.First().Teacher?.Department?.Name ?? "—",
                    NationalId  = g.First().Teacher?.NationalId ?? "—",
                    Total      = g.Count(),
                    Approved   = g.Count(r => r.Status == ReregistrationStatus.Approved),
                    Rejected   = g.Count(r => r.Status == ReregistrationStatus.Rejected),
                    Pending    = g.Count(r => r.Status == ReregistrationStatus.Pending),
                    Last       = g.Max(r => r.RequestedAt)
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            using var ms = new System.IO.MemoryStream();
            using var pdfWriter = new PdfWriter(ms);
            using var pdfDoc = new PdfDocument(pdfWriter);
            pdfDoc.SetDefaultPageSize(iText.Kernel.Geom.PageSize.A4.Rotate());
            var doc = new Document(pdfDoc, iText.Kernel.Geom.PageSize.A4.Rotate());
            doc.SetMargins(30, 30, 40, 40);

            var titleFont  = PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA_BOLD);
            var subFont    = PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);
            var headerFont = PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA_BOLD);
            var cellFont   = PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);

            var gray = new DeviceRgb(128, 128, 128);

            doc.Add(new Paragraph("Device Re-registration — Per-Teacher Summary")
                .SetFont(titleFont).SetFontSize(16).SetMarginBottom(4));
            doc.Add(new Paragraph($"Generated: {DateTime.Now:dd MMM yyyy HH:mm}  |  Total teachers: {byTeacher.Count}")
                .SetFont(subFont).SetFontSize(9).SetFontColor(gray).SetMarginBottom(16));

            var table = new Table(UnitValue.CreatePercentArray(new float[] { 20f, 14f, 12f, 8f, 10f, 10f, 9f, 14f }))
                .UseAllAvailableWidth();

            var headerBg = new DeviceRgb(30, 41, 59);
            string[] headers = { "Teacher", "Department", "National ID", "Total", "Approved", "Rejected", "Pending", "Last Request" };
            foreach (var h in headers)
            {
                table.AddHeaderCell(new Cell()
                    .Add(new Paragraph(h).SetFont(headerFont).SetFontColor(ColorConstants.WHITE))
                    .SetBackgroundColor(headerBg)
                    .SetPadding(6)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(new SolidBorder(new DeviceRgb(51, 65, 85), 0.5f)));
            }

            var altBg = new DeviceRgb(248, 250, 252);
            int row = 0;
            foreach (var t in byTeacher)
            {
                var bg = row++ % 2 == 0 ? ColorConstants.WHITE : altBg;

                AddCell(table, t.Name, cellFont, bg, TextAlignment.LEFT);
                AddCell(table, t.Department, cellFont, bg, TextAlignment.LEFT);
                AddCell(table, t.NationalId, cellFont, bg, TextAlignment.CENTER);
                AddCell(table, t.Total.ToString(), cellFont, bg, TextAlignment.CENTER);
                AddCell(table, t.Approved.ToString(), cellFont, bg, TextAlignment.CENTER);
                AddCell(table, t.Rejected.ToString(), cellFont, bg, TextAlignment.CENTER);
                AddCell(table, t.Pending.ToString(), cellFont, bg, TextAlignment.CENTER);
                AddCell(table, t.Last.ToString("dd MMM yyyy HH:mm"), cellFont, bg, TextAlignment.CENTER);
            }

            doc.Add(table);
            doc.Close();
            return ms.ToArray();
        }

        public byte[] GenerateAuditPdf(List<DeviceReregistrationRequest> requests)
        {
            var sorted = requests.OrderByDescending(r => r.RequestedAt).ToList();

            using var ms = new System.IO.MemoryStream();
            using var pdfWriter = new PdfWriter(ms);
            using var pdfDoc = new PdfDocument(pdfWriter);
            var doc = new Document(pdfDoc, iText.Kernel.Geom.PageSize.A4.Rotate());
            doc.SetMargins(30, 30, 40, 40);

            var titleFont  = PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA_BOLD);
            var subFont    = PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);
            var headerFont = PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA_BOLD);
            var cellFont   = PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);

            var gray = new DeviceRgb(128, 128, 128);

            doc.Add(new Paragraph("Device Re-registration — Full Audit Log")
                .SetFont(titleFont).SetFontSize(16).SetMarginBottom(4));
            doc.Add(new Paragraph($"Generated: {DateTime.Now:dd MMM yyyy HH:mm}  |  Total records: {sorted.Count}")
                .SetFont(subFont).SetFontSize(9).SetFontColor(gray).SetMarginBottom(16));

            var table = new Table(UnitValue.CreatePercentArray(new float[] { 18f, 13f, 14f, 13f, 9f, 14f, 8f, 15f }))
                .UseAllAvailableWidth();

            var headerBg = new DeviceRgb(30, 41, 59);
            string[] headers = { "Teacher", "Department", "Requested At", "Request IP", "Status", "Reviewed At", "National ID", "Note" };
            foreach (var h in headers)
            {
                table.AddHeaderCell(new Cell()
                    .Add(new Paragraph(h).SetFont(headerFont).SetFontColor(ColorConstants.WHITE))
                    .SetBackgroundColor(headerBg)
                    .SetPadding(6)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(new SolidBorder(new DeviceRgb(51, 65, 85), 0.5f)));
            }

            var altBg = new DeviceRgb(248, 250, 252);
            int row = 0;
            foreach (var req in sorted)
            {
                var bg = row++ % 2 == 0 ? ColorConstants.WHITE : altBg;
                AddCell(table, req.Teacher?.FullName ?? "—", cellFont, bg, TextAlignment.LEFT);
                AddCell(table, req.Teacher?.Department?.Name ?? "—", cellFont, bg, TextAlignment.LEFT);
                AddCell(table, req.RequestedAt.ToString("dd MMM yyyy HH:mm"), cellFont, bg, TextAlignment.CENTER);
                AddCell(table, req.RequestedIp, cellFont, bg, TextAlignment.CENTER);
                AddCell(table, req.Status.ToString(), cellFont, bg, TextAlignment.CENTER);
                AddCell(table, req.ReviewedAt.HasValue ? req.ReviewedAt.Value.ToString("dd MMM yyyy HH:mm") : "—", cellFont, bg, TextAlignment.CENTER);
                AddCell(table, req.Teacher?.NationalId ?? "—", cellFont, bg, TextAlignment.CENTER);
                AddCell(table, string.IsNullOrEmpty(req.ReviewNote) ? "—" : req.ReviewNote, cellFont, bg, TextAlignment.LEFT);
            }

            doc.Add(table);
            doc.Close();
            return ms.ToArray();
        }

        private static void AddCell(
            Table table,
            string text,
            PdfFont font,
            Color bg,
            TextAlignment align)
        {
            table.AddCell(new Cell()
                .Add(new Paragraph(text).SetFont(font))
                .SetBackgroundColor(bg)
                .SetPadding(5)
                .SetTextAlignment(align)
                .SetBorder(new SolidBorder(new DeviceRgb(226, 232, 240), 0.5f)));
        }
    }
}
