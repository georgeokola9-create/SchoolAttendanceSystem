using BiometricAttendanceSystem.Models;
using BiometricAttendanceSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace BiometricAttendanceSystem.Data
{
    /// <summary>
    /// One-time demo data seeder.
    /// Run with:  dotnet run -- --seed-demo
    ///
    /// 1. Deletes all existing AttendanceRecords, DeviceReregistrationRequests, and Teachers
    ///    (old data was tied to TscNumber and is no longer valid).
    /// 2. Creates 30 demo teachers spread across the 4 seeded departments,
    ///    each with a unique valid 8-digit National ID.
    /// 3. Generates login credentials for every teacher (username + password).
    /// 4. Generates a full term (13 weeks) of attendance records,
    ///    skipping weekends and configured holidays.
    /// 5. Writes a single PDF with everyone's credentials to
    ///    {ContentRoot}/seed-output/credentials.pdf
    /// </summary>
    public static class DemoDataSeeder
    {
        private static readonly string[] FirstNames =
        {
            "James", "Mary", "Peter", "Grace", "John", "Faith", "David", "Joyce",
            "Samuel", "Esther", "Michael", "Catherine", "Daniel", "Mercy", "Joseph",
            "Agnes", "Stephen", "Lucy", "Paul", "Ann", "George", "Ruth", "Francis",
            "Sarah", "Charles", "Janet", "Patrick", "Susan", "Anthony", "Diana"
        };

        private static readonly string[] LastNames =
        {
            "Mwangi", "Otieno", "Wanjiru", "Kamau", "Achieng", "Kiprotich", "Njoroge",
            "Adhiambo", "Kipchoge", "Wambui", "Omondi", "Chebet", "Mutiso", "Nyambura",
            "Korir", "Wafula", "Akinyi", "Maina", "Cherono", "Odhiambo", "Wairimu",
            "Kiptoo", "Atieno", "Gitau", "Were", "Njeri", "Onyango", "Muthoni",
            "Kibet", "Auma"
        };

        public static async Task RunAsync(IServiceProvider rootServices)
        {
            using var scope   = rootServices.CreateScope();
            var sp            = scope.ServiceProvider;
            var db            = sp.GetRequiredService<AppDbContext>();
            var holidays      = sp.GetRequiredService<IHolidayService>();
            var settingsSvc   = sp.GetRequiredService<ISchoolSettingsService>();
            var authSvc       = sp.GetRequiredService<ITeacherAuthService>();
            var env           = sp.GetRequiredService<IWebHostEnvironment>();

            Console.WriteLine("=== Demo Data Seeder ===");

            // ── 1. Wipe old data ─────────────────────────────────────────
            Console.WriteLine("Removing existing attendance records...");
            db.AttendanceRecords.RemoveRange(db.AttendanceRecords);

            Console.WriteLine("Removing existing device re-registration requests...");
            db.DeviceReregistrationRequests.RemoveRange(db.DeviceReregistrationRequests);

            Console.WriteLine("Removing existing teachers...");
            db.Teachers.RemoveRange(db.Teachers);

            await db.SaveChangesAsync();

            // ── 2. Create 30 teachers ────────────────────────────────────
            var departments = await db.Departments.OrderBy(d => d.Id).ToListAsync();
            if (departments.Count == 0)
                throw new InvalidOperationException("No departments found — run migrations first.");

            var rng        = new Random();
            var usedIds    = new HashSet<string>();
            var termStart  = new DateTime(2026, 1, 5); // Term 1, Monday
            var teachers   = new List<Teacher>();

            for (int i = 0; i < 30; i++)
            {
                var first = FirstNames[i % FirstNames.Length];
                var last  = LastNames[(i * 7) % LastNames.Length];

                string nationalId;
                do { nationalId = rng.Next(10_000_000, 99_999_999).ToString(); }
                while (!usedIds.Add(nationalId));

                teachers.Add(new Teacher
                {
                    FullName       = $"{first} {last}",
                    NationalId     = nationalId,
                    DepartmentId   = departments[i % departments.Count].Id,
                    IsActive       = true,
                    DateRegistered = termStart
                });
            }

            db.Teachers.AddRange(teachers);
            await db.SaveChangesAsync();
            Console.WriteLine($"Created {teachers.Count} teachers.");

            // ── 3. Generate login credentials for everyone ───────────────
            Console.WriteLine("Generating login credentials...");
            foreach (var t in teachers)
                await authSvc.SetCredentialsAsync(t.Id);

            // ── 4. Generate a full term of attendance ────────────────────
            var settings  = await settingsSvc.GetSettingsAsync();
            var expected  = settings.ExpectedCheckInTime; // e.g. 09:00
            var lateLimit = settings.LateArrivalTime;     // e.g. 10:00

            var termEnd = DateTime.Today.AddDays(-1); // up to yesterday (today is handled live by check-ins)
            if (termEnd < termStart) termEnd = termStart;

            Console.WriteLine($"Generating attendance from {termStart:dd MMM yyyy} to {termEnd:dd MMM yyyy} (excluding weekends/holidays)...");

            var records   = new List<AttendanceRecord>();
            var dayCount  = 0;

            // Each teacher gets a "reliability" score so some are consistently
            // punctual and others occasionally late/absent — looks realistic.
            var reliability = teachers.ToDictionary(t => t.Id, _ => 0.85 + rng.NextDouble() * 0.13); // 0.85–0.98

            for (var date = termStart; date <= termEnd; date = date.AddDays(1))
            {
                if (!await holidays.IsWorkingDayAsync(date))
                    continue;

                dayCount++;

                foreach (var teacher in teachers)
                {
                    var reliabilityScore = reliability[teacher.Id];
                    var roll = rng.NextDouble();

                    if (roll > reliabilityScore + 0.05)
                    {
                        // Absent
                        records.Add(new AttendanceRecord
                        {
                            TeacherId = teacher.Id,
                            Date      = date,
                            Status    = AttendanceStatus.Absent,
                            Notes     = "Seeded demo data"
                        });
                    }
                    else if (roll > reliabilityScore)
                    {
                        // Late — between LateLimit and LateLimit + 45 min
                        var lateBy   = rng.Next(1, 45);
                        var checkIn  = lateLimit.Add(TimeSpan.FromMinutes(lateBy));
                        records.Add(new AttendanceRecord
                        {
                            TeacherId   = teacher.Id,
                            Date        = date,
                            CheckInTime = checkIn,
                            Status      = AttendanceStatus.Late,
                            Notes       = "Seeded demo data"
                        });
                    }
                    else
                    {
                        // Present — between 30 min before expected and exactly on time
                        var earlyBy  = rng.Next(0, 30);
                        var checkIn  = expected.Subtract(TimeSpan.FromMinutes(earlyBy));
                        if (checkIn < TimeSpan.Zero) checkIn = TimeSpan.Zero;

                        records.Add(new AttendanceRecord
                        {
                            TeacherId   = teacher.Id,
                            Date        = date,
                            CheckInTime = checkIn,
                            Status      = AttendanceStatus.Present,
                            Notes       = "Seeded demo data"
                        });
                    }
                }
            }

            db.AttendanceRecords.AddRange(records);
            await db.SaveChangesAsync();
            Console.WriteLine($"Inserted {records.Count} attendance records across {dayCount} working days.");

            // ── 5. Write all credential slips to one PDF ──────────────────
            var outputDir = Path.Combine(env.ContentRootPath, "seed-output");
            Directory.CreateDirectory(outputDir);

            var loginUrl  = "http://192.168.1.10"; // change to your server's LAN address
            var pdfBytes  = await authSvc.GenerateAllCredentialSlipsAsync(loginUrl);
            var pdfPath   = Path.Combine(outputDir, "credentials.pdf");
            await File.WriteAllBytesAsync(pdfPath, pdfBytes);

            Console.WriteLine($"Wrote credential slips to: {pdfPath}");
            Console.WriteLine("=== Done ===");
        }
    }
}
