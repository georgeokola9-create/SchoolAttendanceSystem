using System.Diagnostics;
using System.Text.Json;
using BiometricAttendanceSystem.Models.ViewModels;

namespace BiometricAttendanceSystem.Services
{
    public class ChartService : IChartService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ChartService> _logger;

        public ChartService(IWebHostEnvironment env, ILogger<ChartService> logger)
        {
            _env = env;
            _logger = logger;
        }

        public async Task<ChartResultVM> GenerateChartsAsync(ChartDataVM chartData)
        {
            var outputDir = Path.Combine(_env.WebRootPath, "charts");
            Directory.CreateDirectory(outputDir);

            var scriptPath = Path.Combine(_env.ContentRootPath, "PythonCharts", "generate_charts.py");
            if (!File.Exists(scriptPath))
            {
                return new ChartResultVM
                {
                    Success = false,
                    Error = $"Chart script not found at {scriptPath}."
                };
            }

            var jsonData = JsonSerializer.Serialize(chartData, JsonOptions);
            var tempDataPath = Path.Combine(Path.GetTempPath(), $"attendance-charts-{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(tempDataPath, jsonData);

            var psi = new ProcessStartInfo
            {
                FileName = ResolvePythonExecutable(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add("--output");
            psi.ArgumentList.Add(outputDir);
            psi.ArgumentList.Add("--data-file");
            psi.ArgumentList.Add(tempDataPath);

            try
            {
                using var process = Process.Start(psi);
                if (process == null)
                {
                    return new ChartResultVM { Success = false, Error = "Could not start Python." };
                }

                var stdout = await process.StandardOutput.ReadToEndAsync();
                var stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogError("Python chart generation failed: {stderr}", stderr);
                    return new ChartResultVM { Success = false, Error = stderr };
                }

                _logger.LogInformation("Charts generated: {stdout}", stdout);

                var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return new ChartResultVM
                {
                    Success = true,
                    WeeklyBarUrl = $"/charts/weekly_bar.png?t={ts}",
                    PieUrl = $"/charts/status_pie.png?t={ts}",
                    DeptBarUrl = $"/charts/dept_bar.png?t={ts}",
                    TrendLineUrl = $"/charts/trend_line.png?t={ts}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate charts");
                return new ChartResultVM { Success = false, Error = ex.Message };
            }
            finally
            {
                try
                {
                    File.Delete(tempDataPath);
                }
                catch (IOException)
                {
                    // Best effort cleanup only.
                }
            }
        }

        private static string ResolvePythonExecutable()
        {
            return OperatingSystem.IsWindows() ? "python" : "python3";
        }
    }

    public class ChartDataVM
    {
        public List<int> WeeklyPresent { get; set; } = new();
        public List<int> WeeklyLate { get; set; } = new();
        public List<int> WeeklyAbsent { get; set; } = new();
        public int TodayPresent { get; set; }
        public int TodayLate { get; set; }
        public int TodayAbsent { get; set; }
        public List<DeptStat> Departments { get; set; } = new();
        public List<string> TrendWeeks { get; set; } = new();
        public List<double> TrendValues { get; set; } = new();
    }

    public class DeptStat
    {
        public string Name { get; set; } = string.Empty;
        public double Percentage { get; set; }
    }

    public class ChartResultVM
    {
        public bool Success { get; set; }
        public string WeeklyBarUrl { get; set; } = string.Empty;
        public string PieUrl { get; set; } = string.Empty;
        public string DeptBarUrl { get; set; } = string.Empty;
        public string TrendLineUrl { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }
}
