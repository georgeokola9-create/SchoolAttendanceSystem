using BiometricAttendanceSystem.Models;
using BiometricAttendanceSystem.Models.ViewModels;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Borders;
using iText.Layout.Properties;
using ClosedXML.Excel;

namespace BiometricAttendanceSystem.Services
{
    public interface IReportExportService
    {
        Task<byte[]> ExportDailyReportPdfAsync(DailyReportVM report);
        Task<byte[]> ExportDailyReportExcelAsync(DailyReportVM report);
        Task<byte[]> ExportMonthlyReportPdfAsync(MonthlyReportVM report);
        Task<byte[]> ExportMonthlyReportExcelAsync(MonthlyReportVM report);
        Task<byte[]> ExportWeeklyReportPdfAsync(WeeklyReportVM report);
        Task<byte[]> ExportWeeklyReportExcelAsync(WeeklyReportVM report);
        Task<byte[]> ExportIndividualReportPdfAsync(TeacherHistoryVM report);
        Task<byte[]> ExportIndividualReportExcelAsync(TeacherHistoryVM report);
        Task<byte[]> ExportDepartmentReportPdfAsync(DepartmentReportVM report);
        Task<byte[]> ExportDepartmentReportExcelAsync(DepartmentReportVM report);
    }

    public class ReportExportService : IReportExportService
    {
        private readonly ISchoolSettingsService _settingsSvc;

        public ReportExportService(ISchoolSettingsService settingsSvc) => _settingsSvc = settingsSvc;

        // ─────────────────────────────────────────────────────────────────────
        // DAILY REPORT
        // ─────────────────────────────────────────────────────────────────────

        public async Task<byte[]> ExportDailyReportPdfAsync(DailyReportVM report)
        {
            var settings = await _settingsSvc.GetSettingsAsync();

            using var ms = new MemoryStream();
            var pdfWriter  = new PdfWriter(ms);
            var pdfDoc     = new PdfDocument(pdfWriter);
            var doc        = new Document(pdfDoc, PageSize.A4.Rotate());
            doc.SetMargins(40, 25, 40, 25);

            AddHeader(doc, settings.SchoolName, "Daily Attendance Report");
            doc.Add(new Paragraph($"Date: {report.Date:dddd, dd MMMM yyyy}")
                .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA))
                .SetFontSize(11)
                .SetTextAlignment(TextAlignment.CENTER));
            doc.Add(new Paragraph(" "));

            var present = report.Records.Count(r => r.Status == AttendanceStatus.Present);
            var late    = report.Records.Count(r => r.Status == AttendanceStatus.Late);
            var absent  = report.Records.Count(r => r.Status == AttendanceStatus.Absent);
            var total   = report.Records.Count;

            var summaryTable = new Table(UnitValue.CreatePercentArray(new float[] { 1f, 1f, 1f, 1f }))
                .SetWidth(UnitValue.CreatePercentValue(60))
                .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                .SetMarginTop(10)
                .SetMarginBottom(20);

            AddTableHeader(summaryTable, new[] { "Present", "Late", "Absent", "Total" });
            summaryTable.AddCell(CreateSummaryCell(present.ToString(), new DeviceRgb(198, 239, 206)));
            summaryTable.AddCell(CreateSummaryCell(late.ToString(),    new DeviceRgb(255, 235, 156)));
            summaryTable.AddCell(CreateSummaryCell(absent.ToString(),  new DeviceRgb(255, 199, 206)));
            summaryTable.AddCell(CreateSummaryCell(total.ToString(),   new DeviceRgb(221, 221, 221)));
            doc.Add(summaryTable);

            var table = new Table(UnitValue.CreatePercentArray(new float[] { 0.5f, 3f, 1.8f, 2.2f, 1.6f, 1.2f }))
                .UseAllAvailableWidth()
                .SetWidth(UnitValue.CreatePercentValue(100));

            AddTableHeader(table, new[] { "#", "Teacher Name", "National ID", "Department", "Check-In Time", "Status" });

            int rowNum = 1;
            foreach (var r in report.Records.OrderBy(x => x.Teacher?.FullName ?? string.Empty))
            {
                table.AddCell(CreateBodyCell(rowNum.ToString(), centered: true));
                table.AddCell(CreateBodyCell(r.Teacher?.FullName ?? "-"));
                table.AddCell(CreateBodyCell(r.Teacher?.NationalId ?? "-"));
                table.AddCell(CreateBodyCell(r.Teacher?.Department?.Name ?? "-"));
                table.AddCell(CreateBodyCell(r.CheckInDisplay, centered: true));
                table.AddCell(CreateStatusCell(r.Status));
                rowNum++;
            }

            doc.Add(table);
            AddFooter(doc, "Daily Report");
            doc.Close();
            return ms.ToArray();
        }

        public async Task<byte[]> ExportDailyReportExcelAsync(DailyReportVM report)
        {
            var settings = await _settingsSvc.GetSettingsAsync();
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Daily Report");

            ws.Cell("A1").Value = settings.SchoolName;
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 14;

            ws.Cell("A2").Value = $"Daily Attendance Report - {report.Date:dddd, dd MMMM yyyy}";
            ws.Cell("A2").Style.Font.Bold = true;
            ws.Cell("A2").Style.Font.FontSize = 12;

            int row = 4;
            ws.Cell($"A{row}").Value = "Summary";
            ws.Cell($"A{row}").Style.Font.Bold = true;

            var present = report.Records.Count(r => r.Status == AttendanceStatus.Present);
            var late    = report.Records.Count(r => r.Status == AttendanceStatus.Late);
            var absent  = report.Records.Count(r => r.Status == AttendanceStatus.Absent);

            foreach (var (label, value) in new[] {
                ("Present:", present), ("Late:", late),
                ("Absent:", absent),   ("Total:", report.Records.Count) })
            {
                row++;
                ws.Cell($"A{row}").Value = label;
                ws.Cell($"A{row}").Style.Font.Bold = true;
                ws.Cell($"B{row}").Value = value;
            }

            row += 2;
            string[] headers = { "Teacher Name", "National ID", "Department", "Check-In Time", "Status" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(row, i + 1).Value = headers[i];

            var headerRange = ws.Range(row, 1, row, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(200, 200, 200);
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            row++;
            foreach (var r in report.Records.OrderBy(x => x.Teacher?.FullName ?? string.Empty))
            {
                ws.Cell(row, 1).Value = r.Teacher?.FullName ?? "-";
                ws.Cell(row, 2).Value = r.Teacher?.NationalId ?? "-";
                ws.Cell(row, 3).Value = r.Teacher?.Department?.Name ?? "-";
                ws.Cell(row, 4).Value = r.CheckInDisplay;
                ws.Cell(row, 5).Value = r.Status.ToString();
                ws.Cell(row, 5).Style.Fill.BackgroundColor = r.Status switch
                {
                    AttendanceStatus.Present => XLColor.FromArgb(198, 239, 206),
                    AttendanceStatus.Late    => XLColor.FromArgb(255, 235, 156),
                    AttendanceStatus.Absent  => XLColor.FromArgb(255, 199, 206),
                    _                        => XLColor.NoColor
                };
                row++;
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        // ─────────────────────────────────────────────────────────────────────
        // WEEKLY REPORT
        // ─────────────────────────────────────────────────────────────────────

        public async Task<byte[]> ExportWeeklyReportPdfAsync(WeeklyReportVM report)
        {
            var settings = await _settingsSvc.GetSettingsAsync();

            using var ms = new MemoryStream();
            var pdfWriter  = new PdfWriter(ms);
            var pdfDoc     = new PdfDocument(pdfWriter);
            var doc        = new Document(pdfDoc, PageSize.A4.Rotate());
            doc.SetMargins(40, 25, 40, 25);

            AddHeader(doc, settings.SchoolName, "Weekly Attendance Report");
            doc.Add(new Paragraph($"Week: {report.WeekStart:dd MMM yyyy} to {report.WeekEnd:dd MMM yyyy}")
                .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA))
                .SetFontSize(11)
                .SetTextAlignment(TextAlignment.CENTER));
            doc.Add(new Paragraph(" "));

            var present = report.Records.Count(r => r.Status == AttendanceStatus.Present);
            var late    = report.Records.Count(r => r.Status == AttendanceStatus.Late);
            var absent  = report.Records.Count(r => r.Status == AttendanceStatus.Absent);

            var summaryTable = new Table(UnitValue.CreatePercentArray(new float[] { 1f, 1f, 1f, 1f }))
                .SetWidth(UnitValue.CreatePercentValue(60))
                .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                .SetMarginTop(10)
                .SetMarginBottom(20);

            AddTableHeader(summaryTable, new[] { "Present", "Late", "Absent", "Total" });
            summaryTable.AddCell(CreateSummaryCell(present.ToString(),                   new DeviceRgb(198, 239, 206)));
            summaryTable.AddCell(CreateSummaryCell(late.ToString(),                      new DeviceRgb(255, 235, 156)));
            summaryTable.AddCell(CreateSummaryCell(absent.ToString(),                    new DeviceRgb(255, 199, 206)));
            summaryTable.AddCell(CreateSummaryCell(report.Records.Count.ToString(),      new DeviceRgb(221, 221, 221)));
            doc.Add(summaryTable);

            var table = new Table(UnitValue.CreatePercentArray(new float[] { 0.5f, 1.8f, 2.6f, 1.4f, 2f, 1.3f, 1.1f }))
                .UseAllAvailableWidth()
                .SetWidth(UnitValue.CreatePercentValue(100));

            AddTableHeader(table, new[] { "#", "Date", "Teacher Name", "National ID", "Department", "Check-In Time", "Status" });

            int rowNum = 1;
            foreach (var r in report.Records.OrderBy(x => x.Date).ThenBy(x => x.Teacher?.FullName ?? string.Empty))
            {
                table.AddCell(CreateBodyCell(rowNum.ToString(), centered: true));
                table.AddCell(CreateBodyCell(r.Date.ToString("ddd, dd MMM yyyy")));
                table.AddCell(CreateBodyCell(r.Teacher?.FullName ?? "-"));
                table.AddCell(CreateBodyCell(r.Teacher?.NationalId ?? "-"));
                table.AddCell(CreateBodyCell(r.Teacher?.Department?.Name ?? "-"));
                table.AddCell(CreateBodyCell(r.CheckInDisplay, centered: true));
                table.AddCell(CreateStatusCell(r.Status));
                rowNum++;
            }

            doc.Add(table);
            AddFooter(doc, "Weekly Report");
            doc.Close();
            return ms.ToArray();
        }

        public async Task<byte[]> ExportWeeklyReportExcelAsync(WeeklyReportVM report)
        {
            var settings = await _settingsSvc.GetSettingsAsync();
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Weekly Report");

            ws.Cell("A1").Value = settings.SchoolName;
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 14;

            ws.Cell("A2").Value = $"Weekly Attendance Report - {report.WeekStart:dd MMM yyyy} to {report.WeekEnd:dd MMM yyyy}";
            ws.Cell("A2").Style.Font.Bold = true;
            ws.Cell("A2").Style.Font.FontSize = 12;

            int row = 4;
            string[] headers = { "Date", "Teacher Name", "National ID", "Department", "Check-In Time", "Status" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(row, i + 1).Value = headers[i];

            var headerRange = ws.Range(row, 1, row, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(200, 200, 200);
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            row++;
            foreach (var r in report.Records.OrderBy(x => x.Date).ThenBy(x => x.Teacher?.FullName ?? string.Empty))
            {
                ws.Cell(row, 1).Value = r.Date.Date;
                ws.Cell(row, 1).Style.DateFormat.Format = "dd/mm/yyyy";
                ws.Cell(row, 2).Value = r.Teacher?.FullName ?? "-";
                ws.Cell(row, 3).Value = r.Teacher?.NationalId ?? "-";
                ws.Cell(row, 4).Value = r.Teacher?.Department?.Name ?? "-";
                ws.Cell(row, 5).Value = r.CheckInDisplay;
                ws.Cell(row, 6).Value = r.Status.ToString();
                ws.Cell(row, 6).Style.Fill.BackgroundColor = r.Status switch
                {
                    AttendanceStatus.Present => XLColor.FromArgb(198, 239, 206),
                    AttendanceStatus.Late    => XLColor.FromArgb(255, 235, 156),
                    AttendanceStatus.Absent  => XLColor.FromArgb(255, 199, 206),
                    _                        => XLColor.NoColor
                };
                row++;
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        // ─────────────────────────────────────────────────────────────────────
        // MONTHLY REPORT
        // ─────────────────────────────────────────────────────────────────────

        public async Task<byte[]> ExportMonthlyReportPdfAsync(MonthlyReportVM report)
        {
            var settings = await _settingsSvc.GetSettingsAsync();

            using var ms = new MemoryStream();
            var pdfWriter  = new PdfWriter(ms);
            var pdfDoc     = new PdfDocument(pdfWriter);
            var doc        = new Document(pdfDoc, PageSize.A4.Rotate());
            doc.SetMargins(40, 25, 40, 25);

            AddHeader(doc, settings.SchoolName, "Monthly Attendance Report");
            doc.Add(new Paragraph($"{report.MonthName} {report.Year}")
                .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA))
                .SetFontSize(11)
                .SetTextAlignment(TextAlignment.CENTER));
            doc.Add(new Paragraph(" "));

            var totalPresent  = report.Summaries.Sum(x => x.PresentDays);
            var totalLate     = report.Summaries.Sum(x => x.LateDays);
            var totalAbsent   = report.Summaries.Sum(x => x.AbsentDays);
            var avgAttendance = report.Summaries.Count > 0
                ? Math.Round(report.Summaries.Average(x => x.AttendancePercentage), 2)
                : 0;

            var summaryTable = new Table(UnitValue.CreatePercentArray(new float[] { 1f, 1f, 1f, 1.4f }))
                .SetWidth(UnitValue.CreatePercentValue(70))
                .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                .SetMarginTop(10)
                .SetMarginBottom(20);

            AddTableHeader(summaryTable, new[] { "Total Present", "Total Late", "Total Absent", "Avg Attendance %" });
            summaryTable.AddCell(CreateSummaryCell(totalPresent.ToString(), new DeviceRgb(198, 239, 206)));
            summaryTable.AddCell(CreateSummaryCell(totalLate.ToString(),    new DeviceRgb(255, 235, 156)));
            summaryTable.AddCell(CreateSummaryCell(totalAbsent.ToString(),  new DeviceRgb(255, 199, 206)));
            summaryTable.AddCell(CreateSummaryCell($"{avgAttendance}%",     new DeviceRgb(221, 221, 221)));
            doc.Add(summaryTable);

            var table = new Table(UnitValue.CreatePercentArray(new float[] { 0.5f, 2.5f, 2f, 1f, 1f, 1f, 1.2f, 1.2f }))
                .UseAllAvailableWidth()
                .SetWidth(UnitValue.CreatePercentValue(100));

            AddTableHeader(table, new[] { "#", "Teacher", "Department", "Present", "Late", "Absent", "Working Days", "Attendance %" });

            int rowNum = 1;
            foreach (var s in report.Summaries.OrderByDescending(x => x.AttendancePercentage))
            {
                table.AddCell(CreateBodyCell(rowNum.ToString(), centered: true));
                table.AddCell(CreateBodyCell(s.Teacher.FullName));
                table.AddCell(CreateBodyCell(s.Teacher.Department?.Name ?? "-"));
                table.AddCell(CreateBodyCell(s.PresentDays.ToString(), centered: true));
                table.AddCell(CreateBodyCell(s.LateDays.ToString(),    centered: true));
                table.AddCell(CreateBodyCell(s.AbsentDays.ToString(),  centered: true));
                table.AddCell(CreateBodyCell(s.WorkingDays.ToString(), centered: true));

                var percColor = s.AttendancePercentage >= 80
                    ? new DeviceRgb(198, 239, 206)
                    : s.AttendancePercentage >= 60
                        ? new DeviceRgb(255, 235, 156)
                        : new DeviceRgb(255, 199, 206);
                table.AddCell(CreateSummaryCell($"{s.AttendancePercentage}%", percColor));
                rowNum++;
            }

            doc.Add(table);
            AddFooter(doc, "Monthly Report");
            doc.Close();
            return ms.ToArray();
        }

        public async Task<byte[]> ExportMonthlyReportExcelAsync(MonthlyReportVM report)
        {
            var settings = await _settingsSvc.GetSettingsAsync();
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Monthly Report");

            ws.Cell("A1").Value = settings.SchoolName;
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 14;

            ws.Cell("A2").Value = $"Monthly Attendance Report - {report.MonthName} {report.Year}";
            ws.Cell("A2").Style.Font.Bold = true;
            ws.Cell("A2").Style.Font.FontSize = 12;

            int row = 4;
            string[] headers = { "Teacher", "Department", "Present Days", "Late Days", "Absent Days", "Working Days", "Attendance %" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(row, i + 1).Value = headers[i];

            var headerRange = ws.Range(row, 1, row, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(200, 200, 200);
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            row++;
            foreach (var s in report.Summaries.OrderByDescending(x => x.AttendancePercentage))
            {
                ws.Cell(row, 1).Value = s.Teacher.FullName;
                ws.Cell(row, 2).Value = s.Teacher.Department?.Name ?? "-";
                ws.Cell(row, 3).Value = s.PresentDays;
                ws.Cell(row, 4).Value = s.LateDays;
                ws.Cell(row, 5).Value = s.AbsentDays;
                ws.Cell(row, 6).Value = s.WorkingDays;
                ws.Cell(row, 7).Value = s.AttendancePercentage;
                ws.Cell(row, 7).Style.NumberFormat.Format = "0.00\"%\"";
                ws.Cell(row, 7).Style.Fill.BackgroundColor = s.AttendancePercentage >= 80
                    ? XLColor.FromArgb(198, 239, 206)
                    : s.AttendancePercentage >= 60
                        ? XLColor.FromArgb(255, 235, 156)
                        : XLColor.FromArgb(255, 199, 206);
                row++;
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        // ─────────────────────────────────────────────────────────────────────
        // INDIVIDUAL REPORT
        // ─────────────────────────────────────────────────────────────────────

        public async Task<byte[]> ExportIndividualReportPdfAsync(TeacherHistoryVM report)
        {
            var settings = await _settingsSvc.GetSettingsAsync();

            using var ms = new MemoryStream();
            var pdfWriter  = new PdfWriter(ms);
            var pdfDoc     = new PdfDocument(pdfWriter);
            var doc        = new Document(pdfDoc, PageSize.A4);
            doc.SetMargins(40, 25, 40, 25);

            AddHeader(doc, settings.SchoolName, "Individual Attendance Report");
            doc.Add(new Paragraph(" "));

            // Teacher info block
            var infoTable = new Table(UnitValue.CreatePercentArray(new float[] { 1.5f, 2.5f }))
                .SetWidth(UnitValue.CreatePercentValue(80))
                .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                .SetMarginBottom(20);

            var boldFont  = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var plainFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            var labelBg   = new DeviceRgb(240, 240, 240);
            var borderCol = new DeviceRgb(200, 200, 200);

            void AddInfoRow(string label, string value)
            {
                infoTable.AddCell(new Cell()
                    .Add(new Paragraph(label).SetFont(boldFont).SetFontSize(9))
                    .SetBackgroundColor(labelBg)
                    .SetPadding(5)
                    .SetBorder(new SolidBorder(borderCol, 0.5f)));
                infoTable.AddCell(new Cell()
                    .Add(new Paragraph(value).SetFont(plainFont).SetFontSize(9))
                    .SetPadding(5)
                    .SetBorder(new SolidBorder(borderCol, 0.5f)));
            }

            AddInfoRow("Teacher Name:", report.Teacher.FullName);
            AddInfoRow("National ID:",  report.Teacher.NationalId);
            AddInfoRow("Department:",   report.Teacher.Department?.Name ?? "-");
            AddInfoRow("Period:",       $"{report.From:dd MMM yyyy} to {report.To:dd MMM yyyy}");
            doc.Add(infoTable);

            // Summary stats
            var summaryTable = new Table(UnitValue.CreatePercentArray(new float[] { 1f, 1f, 1f, 1f, 1.2f }))
                .SetWidth(UnitValue.CreatePercentValue(80))
                .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                .SetMarginBottom(20);

            AddTableHeader(summaryTable, new[] { "Present", "Late", "Absent", "Total Days", "Attendance %" });
            summaryTable.AddCell(CreateSummaryCell(report.PresentDays.ToString(),     new DeviceRgb(198, 239, 206)));
            summaryTable.AddCell(CreateSummaryCell(report.LateDays.ToString(),        new DeviceRgb(255, 235, 156)));
            summaryTable.AddCell(CreateSummaryCell(report.AbsentDays.ToString(),      new DeviceRgb(255, 199, 206)));
            summaryTable.AddCell(CreateSummaryCell(report.TotalDays.ToString(),       new DeviceRgb(221, 221, 221)));
            summaryTable.AddCell(CreateSummaryCell($"{report.AttendancePercentage}%", new DeviceRgb(221, 221, 221)));
            doc.Add(summaryTable);

            var table = new Table(UnitValue.CreatePercentArray(new float[] { 0.5f, 1.7f, 1.2f, 1.6f, 1.2f }))
                .SetWidth(UnitValue.CreatePercentValue(90))
                .SetHorizontalAlignment(HorizontalAlignment.CENTER);

            AddTableHeader(table, new[] { "#", "Date", "Day", "Check-In Time", "Status" });

            int rowNum = 1;
            foreach (var r in report.Records.OrderByDescending(x => x.Date))
            {
                table.AddCell(CreateBodyCell(rowNum.ToString(), centered: true));
                table.AddCell(CreateBodyCell(r.Date.ToString("dd MMM yyyy"), centered: true));
                table.AddCell(CreateBodyCell(r.Date.ToString("dddd"), centered: true));
                table.AddCell(CreateBodyCell(r.CheckInDisplay, centered: true));
                table.AddCell(CreateStatusCell(r.Status));
                rowNum++;
            }

            doc.Add(table);
            AddFooter(doc, "Individual Report");
            doc.Close();
            return ms.ToArray();
        }

        public async Task<byte[]> ExportIndividualReportExcelAsync(TeacherHistoryVM report)
        {
            var settings = await _settingsSvc.GetSettingsAsync();
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Attendance History");

            ws.Cell("A1").Value = settings.SchoolName;
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 14;

            ws.Cell("A2").Value = $"Individual Attendance Report - {report.Teacher.FullName}";
            ws.Cell("A2").Style.Font.Bold = true;
            ws.Cell("A2").Style.Font.FontSize = 12;

            ws.Cell("A4").Value = "National ID:";  ws.Cell("A4").Style.Font.Bold = true;
            ws.Cell("B4").Value = report.Teacher.NationalId;

            ws.Cell("A5").Value = "Department:";  ws.Cell("A5").Style.Font.Bold = true;
            ws.Cell("B5").Value = report.Teacher.Department?.Name ?? "-";

            ws.Cell("A6").Value = "Period:";      ws.Cell("A6").Style.Font.Bold = true;
            ws.Cell("B6").Value = $"{report.From:dd MMM yyyy} to {report.To:dd MMM yyyy}";

            int row = 8;
            ws.Cell($"A{row}").Value = "Summary";
            ws.Cell($"A{row}").Style.Font.Bold = true;

            row++;
            ws.Cell($"A{row}").Value = "Present Days:";   ws.Cell($"A{row}").Style.Font.Bold = true;
            ws.Cell($"B{row}").Value = report.PresentDays;

            row++;
            ws.Cell($"A{row}").Value = "Late Days:";      ws.Cell($"A{row}").Style.Font.Bold = true;
            ws.Cell($"B{row}").Value = report.LateDays;

            row++;
            ws.Cell($"A{row}").Value = "Absent Days:";    ws.Cell($"A{row}").Style.Font.Bold = true;
            ws.Cell($"B{row}").Value = report.AbsentDays;

            row++;
            ws.Cell($"A{row}").Value = "Total Days:";     ws.Cell($"A{row}").Style.Font.Bold = true;
            ws.Cell($"B{row}").Value = report.TotalDays;

            row++;
            ws.Cell($"A{row}").Value = "Attendance %:";   ws.Cell($"A{row}").Style.Font.Bold = true;
            ws.Cell($"B{row}").Value = report.AttendancePercentage;
            ws.Cell($"B{row}").Style.NumberFormat.Format = "0.00\"%\"";

            row += 2;
            string[] headers = { "Date", "Check-In Time", "Status" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(row, i + 1).Value = headers[i];

            var headerRange = ws.Range(row, 1, row, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(200, 200, 200);
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            row++;
            foreach (var r in report.Records.OrderByDescending(x => x.Date))
            {
                ws.Cell(row, 1).Value = r.Date.Date;
                ws.Cell(row, 1).Style.DateFormat.Format = "dd/mm/yyyy";
                ws.Cell(row, 2).Value = r.CheckInDisplay;
                ws.Cell(row, 3).Value = r.Status.ToString();
                ws.Cell(row, 3).Style.Fill.BackgroundColor = r.Status switch
                {
                    AttendanceStatus.Present => XLColor.FromArgb(198, 239, 206),
                    AttendanceStatus.Late    => XLColor.FromArgb(255, 235, 156),
                    AttendanceStatus.Absent  => XLColor.FromArgb(255, 199, 206),
                    _                        => XLColor.NoColor
                };
                row++;
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        // ─────────────────────────────────────────────────────────────────────
        // DEPARTMENT REPORT
        // ─────────────────────────────────────────────────────────────────────

        public async Task<byte[]> ExportDepartmentReportPdfAsync(DepartmentReportVM report)
        {
            var settings = await _settingsSvc.GetSettingsAsync();

            using var ms = new MemoryStream();
            var pdfWriter  = new PdfWriter(ms);
            var pdfDoc     = new PdfDocument(pdfWriter);
            var doc        = new Document(pdfDoc, PageSize.A4.Rotate());
            doc.SetMargins(40, 25, 40, 25);

            AddHeader(doc, settings.SchoolName, "Department Attendance Report");
            doc.Add(new Paragraph($"{report.Department.Name} - {report.From:dd MMM yyyy} to {report.To:dd MMM yyyy}")
                .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA))
                .SetFontSize(11)
                .SetTextAlignment(TextAlignment.CENTER));
            doc.Add(new Paragraph(" "));

            var totalPresent  = report.Summaries.Sum(x => x.PresentDays);
            var totalLate     = report.Summaries.Sum(x => x.LateDays);
            var totalAbsent   = report.Summaries.Sum(x => x.AbsentDays);
            var avgAttendance = report.Summaries.Count > 0
                ? Math.Round(report.Summaries.Average(x => x.AttendancePercentage), 2)
                : 0;

            var summaryTable = new Table(UnitValue.CreatePercentArray(new float[] { 1f, 1f, 1f, 1.4f }))
                .SetWidth(UnitValue.CreatePercentValue(70))
                .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                .SetMarginTop(10)
                .SetMarginBottom(20);

            AddTableHeader(summaryTable, new[] { "Total Present", "Total Late", "Total Absent", "Avg Attendance %" });
            summaryTable.AddCell(CreateSummaryCell(totalPresent.ToString(), new DeviceRgb(198, 239, 206)));
            summaryTable.AddCell(CreateSummaryCell(totalLate.ToString(),    new DeviceRgb(255, 235, 156)));
            summaryTable.AddCell(CreateSummaryCell(totalAbsent.ToString(),  new DeviceRgb(255, 199, 206)));
            summaryTable.AddCell(CreateSummaryCell($"{avgAttendance}%",     new DeviceRgb(221, 221, 221)));
            doc.Add(summaryTable);

            var table = new Table(UnitValue.CreatePercentArray(new float[] { 0.5f, 2.8f, 1f, 1f, 1f, 1.3f, 1.3f }))
                .UseAllAvailableWidth()
                .SetWidth(UnitValue.CreatePercentValue(100));

            AddTableHeader(table, new[] { "#", "Teacher", "Present", "Late", "Absent", "Working Days", "Attendance %" });

            int rowNum = 1;
            foreach (var s in report.Summaries.OrderByDescending(x => x.AttendancePercentage))
            {
                table.AddCell(CreateBodyCell(rowNum.ToString(), centered: true));
                table.AddCell(CreateBodyCell(s.Teacher.FullName));
                table.AddCell(CreateBodyCell(s.PresentDays.ToString(), centered: true));
                table.AddCell(CreateBodyCell(s.LateDays.ToString(),    centered: true));
                table.AddCell(CreateBodyCell(s.AbsentDays.ToString(),  centered: true));
                table.AddCell(CreateBodyCell(s.WorkingDays.ToString(), centered: true));

                var percColor = s.AttendancePercentage >= 80
                    ? new DeviceRgb(198, 239, 206)
                    : s.AttendancePercentage >= 60
                        ? new DeviceRgb(255, 235, 156)
                        : new DeviceRgb(255, 199, 206);
                table.AddCell(CreateSummaryCell($"{s.AttendancePercentage}%", percColor));
                rowNum++;
            }

            doc.Add(table);
            AddFooter(doc, "Department Report");
            doc.Close();
            return ms.ToArray();
        }

        public async Task<byte[]> ExportDepartmentReportExcelAsync(DepartmentReportVM report)
        {
            var settings = await _settingsSvc.GetSettingsAsync();
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Department Report");

            ws.Cell("A1").Value = settings.SchoolName;
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 14;

            ws.Cell("A2").Value = $"Department Attendance Report - {report.Department.Name}";
            ws.Cell("A2").Style.Font.Bold = true;
            ws.Cell("A2").Style.Font.FontSize = 12;

            ws.Cell("A3").Value = $"Period: {report.From:dd MMM yyyy} to {report.To:dd MMM yyyy}";
            ws.Cell("A3").Style.Font.Bold = true;

            int row = 5;
            string[] headers = { "Teacher", "Present Days", "Late Days", "Absent Days", "Working Days", "Attendance %" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(row, i + 1).Value = headers[i];

            var headerRange = ws.Range(row, 1, row, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(200, 200, 200);
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            row++;
            foreach (var s in report.Summaries.OrderByDescending(x => x.AttendancePercentage))
            {
                ws.Cell(row, 1).Value = s.Teacher.FullName;
                ws.Cell(row, 2).Value = s.PresentDays;
                ws.Cell(row, 3).Value = s.LateDays;
                ws.Cell(row, 4).Value = s.AbsentDays;
                ws.Cell(row, 5).Value = s.WorkingDays;
                ws.Cell(row, 6).Value = s.AttendancePercentage;
                ws.Cell(row, 6).Style.NumberFormat.Format = "0.00\"%\"";
                ws.Cell(row, 6).Style.Fill.BackgroundColor = s.AttendancePercentage >= 80
                    ? XLColor.FromArgb(198, 239, 206)
                    : s.AttendancePercentage >= 60
                        ? XLColor.FromArgb(255, 235, 156)
                        : XLColor.FromArgb(255, 199, 206);
                row++;
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private static void AddHeader(Document doc, string schoolName, string title)
        {
            doc.Add(new Paragraph(schoolName)
                .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                .SetFontSize(16)
                .SetTextAlignment(TextAlignment.CENTER));
            doc.Add(new Paragraph(title)
                .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                .SetFontSize(13)
                .SetTextAlignment(TextAlignment.CENTER));
            doc.Add(new Paragraph($"Generated: {DateTime.Now:dd MMMM yyyy HH:mm:ss}")
                .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA))
                .SetFontSize(9)
                .SetFontColor(ColorConstants.GRAY)
                .SetTextAlignment(TextAlignment.CENTER));
        }

        private static void AddFooter(Document doc, string type)
        {
            doc.Add(new Paragraph(" "));
            doc.Add(new Paragraph($"{type} | Page generated on {DateTime.Now:dd/MM/yyyy HH:mm}")
                .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA))
                .SetFontSize(8)
                .SetFontColor(ColorConstants.GRAY)
                .SetTextAlignment(TextAlignment.CENTER));
        }

        /// <summary>Dark header row for any table.</summary>
        private static void AddTableHeader(Table table, string[] headers)
        {
            var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var bg   = new DeviceRgb(60, 60, 60);

            foreach (var header in headers)
            {
                table.AddHeaderCell(new Cell()
                    .Add(new Paragraph(header).SetFont(font).SetFontSize(10).SetFontColor(ColorConstants.WHITE))
                    .SetBackgroundColor(bg)
                    .SetPadding(7)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(new SolidBorder(ColorConstants.WHITE, 0.5f)));
            }
        }

        /// <summary>Standard body cell - left-aligned by default, optionally centred.</summary>
        private static Cell CreateBodyCell(string text, bool centered = false)
        {
            return new Cell()
                .Add(new Paragraph(text ?? "-")
                    .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA))
                    .SetFontSize(9))
                .SetPadding(5)
                .SetTextAlignment(centered ? TextAlignment.CENTER : TextAlignment.LEFT)
                .SetBorder(new SolidBorder(new DeviceRgb(200, 200, 200), 0.5f));
        }

        /// <summary>Bold centred cell with a tinted background - used for summary stats and colour-coded values.</summary>
        private static Cell CreateSummaryCell(string text, DeviceRgb bg)
        {
            return new Cell()
                .Add(new Paragraph(text ?? "-")
                    .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                    .SetFontSize(10))
                .SetBackgroundColor(bg)
                .SetPadding(6)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetBorder(new SolidBorder(ColorConstants.WHITE, 0.5f));
        }

        /// <summary>Centred, colour-coded status cell derived from AttendanceStatus enum.</summary>
        private static Cell CreateStatusCell(AttendanceStatus status)
        {
            var (text, bg) = status switch
            {
                AttendanceStatus.Present => ("Present", new DeviceRgb(198, 239, 206)),
                AttendanceStatus.Late    => ("Late",    new DeviceRgb(255, 235, 156)),
                AttendanceStatus.Absent  => ("Absent",  new DeviceRgb(255, 199, 206)),
                _                        => (status.ToString(), new DeviceRgb(240, 240, 240))
            };

            return new Cell()
                .Add(new Paragraph(text)
                    .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                    .SetFontSize(9))
                .SetBackgroundColor(bg)
                .SetPadding(5)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetBorder(new SolidBorder(new DeviceRgb(200, 200, 200), 0.5f));
        }
    }
}
