using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AnimeJaNaiConfEditor.Services
{
    // A single community benchmark result, ready to post to the AnimeJaNai
    // benchmark catalog. The user runs the benchmark (which writes
    // animejanai/benchmark.txt); this gathers the hardware/software context,
    // is shown back to the user verbatim for confirmation, and is POSTed to
    // our own endpoint - no GitHub account or token lives on the client. The
    // server stamps id + submitted_at, so those are not sent.
    public sealed class BenchmarkSubmission
    {
        // Maintainer-operated proxy (holds the GitHub credential, validates,
        // and files the submission as a PR). Catalog is the published site.
        public const string SubmitUrl = "https://animejan.ai/api/benchmarks";
        public const string CatalogUrl = "https://benchmarks.animejan.ai";

        [JsonPropertyName("schema")] public int Schema { get; set; } = 1;
        [JsonPropertyName("app_version")] public string AppVersion { get; set; } = "";
        [JsonPropertyName("backend")] public string Backend { get; set; } = "";
        [JsonPropertyName("gpu")] public string Gpu { get; set; } = "";
        // Accurate total VRAM from nvidia-smi for NVIDIA; omitted otherwise
        // (WMI AdapterRAM is a uint32 that caps at ~4 GB, so it would lie about
        // modern cards - better to leave it blank than report a wrong number).
        [JsonPropertyName("vram_mb")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? VramMb { get; set; }
        // Enforced GPU power limit / TGP in watts (NVIDIA only; the biggest
        // reason results diverge across the "same" GPU - laptop vs desktop).
        [JsonPropertyName("gpu_power_w")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int GpuPowerW { get; set; }
        [JsonPropertyName("cpu")] public string Cpu { get; set; } = "";
        // CPU max clock (MHz) and core/thread counts (omitted when 0/unknown).
        [JsonPropertyName("cpu_mhz")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int CpuMhz { get; set; }
        [JsonPropertyName("cpu_cores")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int CpuCores { get; set; }
        [JsonPropertyName("cpu_threads")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int CpuThreads { get; set; }
        // Total installed RAM and its actual operating speed (omitted when 0/unknown).
        [JsonPropertyName("ram_mb")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long RamMb { get; set; }
        [JsonPropertyName("ram_speed_mhz")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int RamSpeedMhz { get; set; }
        [JsonPropertyName("os")] public string Os { get; set; } = "";
        [JsonPropertyName("driver")] public string Driver { get; set; } = "";
        // template name (Balanced/Performance) -> resolution -> fps
        [JsonPropertyName("results")] public Dictionary<string, Dictionary<string, double>> Results { get; set; } = new();
        // Optional credit: GitHub/Discord handle or any name; blank = anonymous.
        [JsonPropertyName("submitted_by")] public string SubmittedBy { get; set; } = "";
        [JsonPropertyName("note")] public string Note { get; set; } = "";

        static readonly JsonSerializerOptions PreviewOpts = new() { WriteIndented = true };

        public bool HasResults => Results.Values.Any(r => r.Count > 0);

        public string ToPreviewJson() => JsonSerializer.Serialize(this, PreviewOpts);

        // Parse animejanai/benchmark.txt exactly as benchmark.ps1 writes it:
        //
        //   AnimeJaNai inference benchmark - backend: TensorRT
        //   <blank>
        //   |fps|480x360|1280x720|1920x1080|
        //   |-|-|-|-|
        //   |Balanced|1204.5|210.3|78.99|
        //   |Performance|...|
        //
        // Resolution columns come from the header row, not a fixed list, so
        // this keeps working if the bundled seed resolutions change.
        public static BenchmarkSubmission FromBenchmarkFile(string path)
        {
            var sub = new BenchmarkSubmission();
            string[] cols = Array.Empty<string>();
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;

                if (!line.StartsWith("|"))
                {
                    var m = Regex.Match(line, @"backend:\s*(\S+)", RegexOptions.IgnoreCase);
                    if (m.Success) sub.Backend = m.Groups[1].Value;
                    continue;
                }

                var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
                if (cells.Length == 0) continue;
                if (cells[0].Equals("fps", StringComparison.OrdinalIgnoreCase))
                {
                    cols = cells.Skip(1).ToArray();
                    continue;
                }
                if (cells.All(c => c.Length == 0 || c == "-")) continue; // separator

                var row = new Dictionary<string, double>();
                for (int i = 1; i < cells.Length && i - 1 < cols.Length; i++)
                {
                    if (double.TryParse(cells[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var fps))
                        row[cols[i - 1]] = fps;
                }
                if (row.Count > 0) sub.Results[cells[0]] = row;
            }
            return sub;
        }

        // Fill in hardware/software context. Everything here is best-effort:
        // a missing field is left blank rather than blocking the submission.
        // animejanaiDir is the Manager's ExePath (the animejanai/ directory);
        // version.txt lives one level up at the install root.
        public void FillSystemInfo(string animejanaiDir)
        {
            AppVersion = ReadAppVersion(animejanaiDir);
            Os = RuntimeInformation.OSDescription;
            try { GatherFromWmi(); } catch { /* WMI unavailable: leave blank */ }
            TryFillNvidia();
        }

        // Total VRAM (MB) and enforced power limit / TGP (W) from nvidia-smi
        // (NVML under the hood). No-op on non-NVIDIA systems or if nvidia-smi
        // isn't on PATH. With multiple NVIDIA GPUs, the largest-memory one (the
        // card actually doing the upscaling) wins, and its power is taken from
        // the same row.
        void TryFillNvidia()
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments = "--query-gpu=memory.total,power.limit --format=csv,noheader,nounits",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                if (p == null) return;
                var stdout = p.StandardOutput.ReadToEnd();
                p.WaitForExit(4000);
                long bestVram = 0;
                int bestPower = 0;
                foreach (var line in stdout.Split('\n'))
                {
                    var parts = line.Split(',');
                    if (!long.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var vram))
                        continue;
                    if (vram <= bestVram) continue;
                    bestVram = vram;
                    bestPower = parts.Length >= 2 &&
                                double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
                        ? (int)Math.Round(w) : 0;
                }
                if (bestVram > 0) VramMb = bestVram;
                if (bestPower > 0) GpuPowerW = bestPower;
            }
            catch { /* leave blank */ }
        }

        void GatherFromWmi()
        {
            using (var s = new ManagementObjectSearcher("SELECT Name, DriverVersion FROM Win32_VideoController"))
            {
                var gpus = s.Get().Cast<ManagementObject>()
                    .Select(mo => (name: (mo["Name"] as string)?.Trim() ?? "",
                                   driver: (mo["DriverVersion"] as string)?.Trim() ?? ""))
                    .Where(g => g.name.Length > 0
                                && !g.name.Contains("Basic Render", StringComparison.OrdinalIgnoreCase)
                                && !g.name.Contains("Remote Display", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                // Prefer a discrete GPU (the one doing the upscaling) over an
                // integrated adapter when both are present.
                var pick = gpus.FirstOrDefault(g => Regex.IsMatch(g.name,
                    @"NVIDIA|GeForce|RTX|GTX|Radeon|\bAMD\b|Arc\b", RegexOptions.IgnoreCase));
                if (pick.name == null || pick.name.Length == 0) pick = gpus.FirstOrDefault();
                if (pick.name != null && pick.name.Length > 0) { Gpu = pick.name; Driver = pick.driver; }
            }

            using (var s = new ManagementObjectSearcher("SELECT Name, MaxClockSpeed, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor"))
            {
                var cpu = s.Get().Cast<ManagementObject>().FirstOrDefault();
                if (cpu != null)
                {
                    Cpu = (cpu["Name"] as string)?.Trim() ?? "";
                    CpuMhz = (int)ToLong(cpu["MaxClockSpeed"]);
                    CpuCores = (int)ToLong(cpu["NumberOfCores"]);
                    CpuThreads = (int)ToLong(cpu["NumberOfLogicalProcessors"]);
                }
            }

            using (var s = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
            {
                var bytes = s.Get().Cast<ManagementObject>()
                    .Select(mo => ToLong(mo["TotalPhysicalMemory"]))
                    .FirstOrDefault();
                if (bytes > 0) RamMb = (long)Math.Round(bytes / 1048576.0);
            }

            // ConfiguredClockSpeed is the actual operating speed (reflects XMP/EXPO);
            // fall back to the SMBIOS-rated Speed when it's unavailable.
            using (var s = new ManagementObjectSearcher("SELECT Speed, ConfiguredClockSpeed FROM Win32_PhysicalMemory"))
            {
                int best = 0;
                foreach (var mo in s.Get().Cast<ManagementObject>())
                {
                    int configured = (int)ToLong(mo["ConfiguredClockSpeed"]);
                    int rated = (int)ToLong(mo["Speed"]);
                    best = Math.Max(best, configured > 0 ? configured : rated);
                }
                if (best > 0) RamSpeedMhz = best;
            }
        }

        static long ToLong(object? value)
        {
            try { return value == null ? 0L : Convert.ToInt64(value, CultureInfo.InvariantCulture); }
            catch { return 0L; }
        }

        static string ReadAppVersion(string animejanaiDir)
        {
            try
            {
                var versionTxt = Path.GetFullPath(Path.Combine(animejanaiDir, "..", "version.txt"));
                if (File.Exists(versionTxt))
                    return File.ReadAllText(versionTxt).Trim();
            }
            catch { /* ignore */ }
            return "";
        }

        public async Task<(bool ok, string message)> SubmitAsync(CancellationToken ct = default)
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AnimeJaNaiManager");
            using var content = new StringContent(JsonSerializer.Serialize(this), Encoding.UTF8, "application/json");
            try
            {
                var resp = await client.PostAsync(SubmitUrl, content, ct);
                if (resp.IsSuccessStatusCode)
                    return (true, $"Thanks! Your benchmark was submitted.\n\nAfter a quick review it will appear in the community catalog at {CatalogUrl}.");

                var body = (await resp.Content.ReadAsStringAsync(ct)).Trim();
                return (false, $"The server rejected the submission (HTTP {(int)resp.StatusCode}).\n\n{Truncate(body, 300)}");
            }
            catch (Exception ex)
            {
                return (false, $"Couldn't reach the benchmark server: {ex.Message}\n\n" +
                               "Please try again later, or share your benchmark.txt on the AnimeJaNai Discord.");
            }
        }

        static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) ? "(no details)" : (s.Length <= max ? s : s.Substring(0, max) + "...");
    }
}
