using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CrazyOptimizer.Editor.WindowComponents.BuildLogs;

namespace CrazyGames
{
    // One snapshot per analysis; yield in bounded batches instead of once per log line.
    internal sealed class BuildAssetReport
    {
        internal const string SizeTooltip = "Asset bytes in the latest complete Editor.log build report, before build/download compression. Not source-file size or runtime RAM. Rebuild and analyze to refresh; the report may be from another platform/profile. N/A means unavailable. Embedded materials have no separate reported size.";
        internal readonly List<BuildLogTreeItem> Entries = new List<BuildLogTreeItem>();
        private readonly Dictionary<string, double> _sizes = new Dictionary<string, double>(StringComparer.Ordinal);

        internal double? GetSize(string path) => path != null && _sizes.TryGetValue(path, out var size) ? (double?)size : null;

        internal IEnumerable<string> Read(string path, AnalysisJob analysis, bool required = false)
        {
            Entries.Clear();
            _sizes.Clear();
            StreamReader reader = null;
            try
            {
                reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete));
            }
            catch (IOException) { if (required) throw; }
            catch (UnauthorizedAccessException) { if (required) throw; }
            if (reader == null) yield break;
            var lines = new List<string>();
            List<string> completedLines = null;
            bool readingReport = false, readingAssets = false;
            var batchTimer = Stopwatch.StartNew();
            using (reader)
            {
                string line;
                long lineNumber = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    ++lineNumber;
                    string trimmed = line.Trim();
                    if (trimmed == "Build Report" || trimmed.StartsWith("Uncompressed usage by category", StringComparison.Ordinal))
                    {
                        lines.Clear();
                        readingReport = true;
                        readingAssets = false;
                    }
                    else if (readingReport && trimmed.StartsWith("Used Assets and files from the Resources folder", StringComparison.Ordinal))
                        readingAssets = true;
                    else if (readingAssets && trimmed.StartsWith("------------", StringComparison.Ordinal))
                    {
                        readingAssets = false;
                        readingReport = false;
                        completedLines = new List<string>(lines);
                    }
                    else if (readingAssets && trimmed.Length > 0) lines.Add(trimmed);
                    // AnalysisJob caps iterator steps per Editor update. Per-line yields
                    // would throttle even cheap reads to only 100 lines per update.
                    if (lineNumber % 128 == 0 && (lineNumber % 2048 == 0 || batchTimer.ElapsedMilliseconds >= 4))
                    {
                        yield return $"Reading build sizes: line {lineNumber}";
                        batchTimer.Restart();
                    }
                }
            }
            if (completedLines == null)
            {
                if (required) throw new InvalidDataException("No complete asset report was found in Editor.log. Build the project and try again");
                yield break;
            }
            int id = 0;
            double total = 0;
            yield return $"Parsing build sizes: 0/{completedLines.Count}";
            batchTimer.Restart();
            foreach (string line in completedLines)
            {
                ++id;
                try
                {
                    var entry = ParseAssetLine(line, id);
                    Entries.Add(entry);
                    total += entry.sizeInBytes;
                    _sizes.TryGetValue(entry.filePath, out var previous);
                    _sizes[entry.filePath] = previous + entry.sizeInBytes;
                }
                catch (FormatException exception) { analysis?.ReportSkipped(line, exception); }
                if (id % 64 == 0 && (id % 512 == 0 || batchTimer.ElapsedMilliseconds >= 4))
                {
                    yield return $"Parsing build sizes: {id}/{completedLines.Count}";
                    batchTimer.Restart();
                }
            }
            foreach (var entry in Entries)
                entry.sizePercentage = total > 0 ? (float)(100 * entry.sizeInBytes / total) : 0;
        }

        internal static BuildLogTreeItem ParseAssetLine(string line, int id)
        {
            var parts = line.Split(new[] { ' ', '\t' }, 4, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4 || !parts[2].EndsWith("%", StringComparison.Ordinal) ||
                !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var size) ||
                !float.TryParse(parts[2].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var percentage) ||
                float.IsNaN(size) || float.IsInfinity(size) || size < 0 ||
                float.IsNaN(percentage) || float.IsInfinity(percentage) || percentage < 0)
                throw new FormatException("Invalid asset size or percentage");
            string unit = parts[1].ToLowerInvariant();
            if (unit != "b" && unit != "kb" && unit != "mb" && unit != "gb")
                throw new FormatException("Unsupported size unit: " + parts[1]);
            string path = parts[3].Trim();
            if (path.Length == 0) throw new FormatException("Missing asset path");
            var entry = new BuildLogTreeItem("BuildLogLine", 0, id, size, unit, percentage, path);
            if (entry.sizeInBytes >= long.MaxValue) throw new FormatException("Asset size is too large");
            return entry;
        }
    }
}
