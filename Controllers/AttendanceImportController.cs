using BiometricAttendanceSystem.Models;
using BiometricAttendanceSystem.Models.ViewModels;
using BiometricAttendanceSystem.Repositories;
using BiometricAttendanceSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace BiometricAttendanceSystem.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AttendanceImportController : Controller
    {
        private readonly ITeacherRepository    _teacherRepo;
        private readonly IAttendanceRepository _attendanceRepo;
        private readonly IHolidayService       _holidays;

        public AttendanceImportController(
            ITeacherRepository teacherRepo,
            IAttendanceRepository attendanceRepo,
            IHolidayService holidays)
        {
            _teacherRepo    = teacherRepo;
            _attendanceRepo = attendanceRepo;
            _holidays       = holidays;
        }

        // ── Manual Entry ─────────────────────────────────────────────────

        /// <summary>GET /AttendanceImport/Manual</summary>
        public async Task<IActionResult> Manual(DateTime? date)
        {
            var teachers = await _teacherRepo.GetAllAsync();
            var vm = new ManualAttendanceVM
            {
                Date     = date?.Date ?? DateTime.Today,
                Teachers = teachers
            };
            return View(vm);
        }

        /// <summary>POST /AttendanceImport/Manual</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manual(ManualAttendanceVM vm)
        {
            if (!ModelState.IsValid)
            {
                vm.Teachers = await _teacherRepo.GetAllAsync();
                return View(vm);
            }

            var saved   = 0;
            var skipped = 0;
            var errors  = new List<string>();

            foreach (var entry in vm.Entries.Where(e => e.Status != null))
            {
                try
                {
                    var existing = await _attendanceRepo.GetByTeacherAndDateAsync(
                        entry.TeacherId, vm.Date);

                    TimeSpan? checkInTime = null;
                    if (!string.IsNullOrWhiteSpace(entry.CheckInTimeStr) &&
                        TimeSpan.TryParse(entry.CheckInTimeStr, out var parsed))
                        checkInTime = parsed;

                    var status = Enum.Parse<AttendanceStatus>(entry.Status!);

                    if (existing == null)
                    {
                        await _attendanceRepo.AddAsync(new AttendanceRecord
                        {
                            TeacherId   = entry.TeacherId,
                            Date        = vm.Date,
                            CheckInTime = checkInTime,
                            Status      = status,
                            Notes       = "Manual entry (admin)"
                        });
                    }
                    else
                    {
                        existing.CheckInTime = checkInTime;
                        existing.Status      = status;
                        existing.Notes       = "Manual entry (admin)";
                        await _attendanceRepo.UpdateAsync(existing);
                        skipped++;
                    }

                    saved++;
                }
                catch (Exception ex)
                {
                    var teacher = (await _teacherRepo.GetByIdAsync(entry.TeacherId))?.FullName ?? $"ID {entry.TeacherId}";
                    errors.Add($"{teacher}: {ex.Message}");
                }
            }

            TempData["Success"] = $"{saved} records saved for {vm.Date:dd MMM yyyy}." +
                                  (skipped > 0 ? $" {skipped} existing record(s) updated." : "");
            if (errors.Any())
                TempData["Error"] = string.Join("; ", errors);

            return RedirectToAction(nameof(Manual), new { date = vm.Date.ToString("yyyy-MM-dd") });
        }

        // ── CSV Import ────────────────────────────────────────────────────

        /// <summary>GET /AttendanceImport/Import</summary>
        public IActionResult Import() => View();

        /// <summary>POST /AttendanceImport/Import</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Please select a CSV file.");
                return View();
            }

            var results = new List<CsvImportResult>();
            var teachers = await _teacherRepo.GetAllAsync();
            var teacherByName = teachers.ToDictionary(
                t => t.FullName.Trim().ToLowerInvariant(), t => t);
            var teacherByNationalId = teachers.ToDictionary(
                t => t.NationalId.Trim().ToLowerInvariant(), t => t);

            using var reader = new StreamReader(csvFile.OpenReadStream());
            var lineNum = 0;
            string? line;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                lineNum++;

                // Skip header row
                if (lineNum == 1) continue;
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(',');
                if (cols.Length < 3)
                {
                    results.Add(CsvImportResult.Fail(lineNum, line, "Row has fewer than 3 columns."));
                    continue;
                }

                // Column order: Date, Teacher Name or National ID, Status, Check-In Time (optional)
                var dateStr      = cols[0].Trim().Trim('"');
                var teacherStr   = cols[1].Trim().Trim('"').ToLowerInvariant();
                var statusStr    = cols[2].Trim().Trim('"');
                var timeStr      = cols.Length > 3 ? cols[3].Trim().Trim('"') : "";

                // Parse date
                if (!DateTime.TryParseExact(dateStr, new[] { "dd/MM/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d/M/yyyy" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    results.Add(CsvImportResult.Fail(lineNum, line, $"Could not parse date '{dateStr}'. Use dd/MM/yyyy."));
                    continue;
                }

                // Find teacher
                Teacher? teacher = null;
                if (teacherByName.TryGetValue(teacherStr, out var byName))
                    teacher = byName;
                else if (teacherByNationalId.TryGetValue(teacherStr, out var byId))
                    teacher = byId;

                if (teacher == null)
                {
                    results.Add(CsvImportResult.Fail(lineNum, line,
                        $"Teacher '{cols[1].Trim()}' not found. Use exact full name or National ID."));
                    continue;
                }

                // Parse status
                if (!Enum.TryParse<AttendanceStatus>(statusStr, true, out var status))
                {
                    results.Add(CsvImportResult.Fail(lineNum, line,
                        $"Invalid status '{statusStr}'. Use Present, Late, or Absent."));
                    continue;
                }

                // Parse optional check-in time
                TimeSpan? checkInTime = null;
                if (!string.IsNullOrWhiteSpace(timeStr) &&
                    TimeSpan.TryParse(timeStr, out var parsedTime))
                    checkInTime = parsedTime;

                // Save
                try
                {
                    var existing = await _attendanceRepo.GetByTeacherAndDateAsync(teacher.Id, date);
                    var isUpdate = existing != null;

                    if (existing == null)
                    {
                        await _attendanceRepo.AddAsync(new AttendanceRecord
                        {
                            TeacherId   = teacher.Id,
                            Date        = date,
                            CheckInTime = checkInTime,
                            Status      = status,
                            Notes       = "CSV import (admin)"
                        });
                    }
                    else
                    {
                        existing.CheckInTime = checkInTime;
                        existing.Status      = status;
                        existing.Notes       = "CSV import (admin)";
                        await _attendanceRepo.UpdateAsync(existing);
                    }

                    results.Add(CsvImportResult.Ok(lineNum,
                        teacher.FullName, date, status, isUpdate));
                }
                catch (Exception ex)
                {
                    results.Add(CsvImportResult.Fail(lineNum, line, ex.Message));
                }
            }

            return View("ImportResults", results);
        }

        /// <summary>GET /AttendanceImport/DownloadTemplate — returns the CSV template</summary>
        public IActionResult DownloadTemplate()
        {
            var csv = "Date,Teacher Name or National ID,Status,Check-In Time (optional)\r\n" +
                      "05/01/2026,James Mwangi,Present,07:45\r\n" +
                      "05/01/2026,Mary Otieno,Late,09:20\r\n" +
                      "05/01/2026,12345678,Absent,\r\n";

            return File(
                System.Text.Encoding.UTF8.GetBytes(csv),
                "text/csv",
                "attendance_import_template.csv");
        }
    }

    // ── ViewModels ────────────────────────────────────────────────────────

    public class ManualAttendanceVM
    {
        public DateTime          Date     { get; set; } = DateTime.Today;
        public List<Teacher>     Teachers { get; set; } = new();
        public List<ManualEntry> Entries  { get; set; } = new();
    }

    public class ManualEntry
    {
        public int     TeacherId      { get; set; }
        public string? Status         { get; set; }   // null = no entry set
        public string? CheckInTimeStr { get; set; }   // "07:45"
    }

    public class CsvImportResult
    {
        public int     Line        { get; set; }
        public bool    Success     { get; set; }
        public string  TeacherName { get; set; } = "";
        public string  Date        { get; set; } = "";
        public string  Status      { get; set; } = "";
        public bool    WasUpdate   { get; set; }
        public string  RawLine     { get; set; } = "";
        public string  Error       { get; set; } = "";

        public static CsvImportResult Ok(int line, string name,
            DateTime date, AttendanceStatus status, bool wasUpdate) => new()
        {
            Line        = line,
            Success     = true,
            TeacherName = name,
            Date        = date.ToString("dd MMM yyyy"),
            Status      = status.ToString(),
            WasUpdate   = wasUpdate
        };

        public static CsvImportResult Fail(int line, string raw, string error) => new()
        {
            Line    = line,
            Success = false,
            RawLine = raw,
            Error   = error
        };
    }
}
