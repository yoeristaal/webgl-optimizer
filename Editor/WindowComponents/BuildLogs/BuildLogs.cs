using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CrazyGames;
using CrazyGames.TreeLib;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CrazyOptimizer.Editor.WindowComponents.BuildLogs
{
    public class BuildLogs
    {
        private static MultiColumnHeaderState _multiColumnHeaderState;
        private static BuildLogTree _buildLogTree;
        private static readonly AnalysisJob _analysis = new AnalysisJob();

        private static bool _includeFilesFromPackages;

        public static void RenderGUI()
        {
            var rect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(300));
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Press \"Analyze build logs\" button, but be sure the project was built at least once on this machine.");
            GUILayout.Label("Press it again when you need to refresh the data.");
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            _buildLogTree?.OnGUI(rect);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_analysis.IsRunning ? "Cancel analysis" : "Analyze build logs", GUILayout.Width(200)))
            {
                if (_analysis.IsRunning)
                    _analysis.Cancel();
                else
                    AnalyzeBuildLogs();
            }

            var originalValue = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 160;
            using (new EditorGUI.DisabledScope(_analysis.IsRunning))
                _includeFilesFromPackages = EditorGUILayout.Toggle("Include files from Packages", _includeFilesFromPackages);
            EditorGUIUtility.labelWidth = originalValue;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Open Editor.log", GUILayout.Width(200)))
            {
                var processName = Application.platform == RuntimePlatform.OSXEditor ? "open" : Application.platform == RuntimePlatform.LinuxEditor ? "xdg-open" : "notepad.exe";
                Process.Start(processName, GetEditorLogPath());
            }

            EditorGUILayout.EndHorizontal();

            _analysis.RenderStatus();

            EditorGUILayout.Space(5);

            GUILayout.Label(
                "This utility analyzes the Build Report from the Editor.log file. It will display all the files included in your final build, and the memory they occupy. You can use this utility to detect more opportunities to decrease the final build size. There may be textures that still occupy a lot of memory, uncompressed sounds, or stuff forgotten in the Resources folders that gets included in the build.",
                EditorStyles.wordWrappedLabel);
        }

        private static string GetEditorLogPath() => Application.consoleLogPath;

        internal static void CancelAnalysis() => _analysis.Cancel();

        static void AnalyzeBuildLogs()
        {
            bool includePackages = _includeFilesFromPackages;
            _analysis.Start(() => AnalyzeBuildLogsSteps(includePackages));
        }

        static IEnumerator<string> AnalyzeBuildLogsSteps(bool includePackages)
        {
            yield return "Reading the latest build report...";
            var reportLines = new List<string>();
            bool readingReport = false;
            bool readingAssets = false;
            bool complete = false;
            long lineNumber = 0;
            // Stream the log rather than copying and loading the entire file. Iterator
            // disposal closes the stream on cancellation, failure, and assembly reload.
            using (var stream = new FileStream(GetEditorLogPath(), FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return $"Reading Editor.log: line {++lineNumber}";
                    string trimmed = line.Trim();
                    if (trimmed == "Build Report" || trimmed.StartsWith("Uncompressed usage by category", StringComparison.Ordinal))
                    {
                        reportLines.Clear();
                        readingReport = true;
                        readingAssets = false;
                        complete = false;
                    }
                    else if (readingReport && trimmed.StartsWith("Used Assets and files from the Resources folder", StringComparison.Ordinal))
                    {
                        readingAssets = true;
                    }
                    else if (readingAssets && trimmed.StartsWith("------------", StringComparison.Ordinal))
                    {
                        readingAssets = false;
                        readingReport = false;
                        complete = true;
                    }
                    else if (readingAssets && trimmed.Length > 0)
                    {
                        reportLines.Add(trimmed);
                    }
                }
            }
            if (!complete)
                throw new InvalidDataException("No complete asset report was found in Editor.log. Build the project and try again");

            var treeElements = new List<BuildLogTreeItem>();
            treeElements.Add(new BuildLogTreeItem("Root", -1, 0, 0, "", 0, ""));
            int idIncrement = 0;
            foreach (string line in reportLines)
            {
                yield return $"Parsing build assets: {++idIncrement}/{reportLines.Count}";
                try
                {
                    var entry = ParseAssetLine(line, idIncrement);
                    if (entry.filePath.StartsWith("Packages/", StringComparison.Ordinal) && !includePackages)
                        continue;
                    treeElements.Add(entry);
                }
                catch (Exception exception) { _analysis.ReportSkipped(line, exception); }
            }

            yield return "Building results table...";
            var treeModel = new TreeModel<BuildLogTreeItem>(treeElements);
            var treeViewState = new TreeViewState<int>();
            _multiColumnHeaderState = _multiColumnHeaderState ?? new MultiColumnHeaderState(new[]
            {
                // when adding a new column don't forget to check the sorting method, and the CellGUI method
                new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Size"}, width = 80, minWidth = 60, canSort = true},
                new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Size %"}, width = 60, minWidth = 40, canSort = true},
                new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Path"}, width = 300, minWidth = 200, canSort = true},
            });
            _buildLogTree = new BuildLogTree(treeViewState, new MultiColumnHeader(_multiColumnHeaderState), treeModel);

            if (OptimizerWindow.EditorWindowInstance != null)
            {
                OptimizerWindow.EditorWindowInstance.Repaint();
            }
        }
        internal static BuildLogTreeItem ParseAssetLine(string line, int id)
        {
            var parts = line.Split(new[] { ' ', '\t' }, 4, StringSplitOptions.RemoveEmptyEntries);
            float size;
            float percentage;
            if (parts.Length != 4 || !parts[2].EndsWith("%", StringComparison.Ordinal) ||
                !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out size) ||
                !float.TryParse(parts[2].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out percentage) ||
                float.IsNaN(size) || float.IsInfinity(size) || size < 0 ||
                float.IsNaN(percentage) || float.IsInfinity(percentage) || percentage < 0 || percentage > 100)
                throw new FormatException("Invalid asset size or percentage");
            string unit = parts[1].ToLowerInvariant();
            if (unit != "b" && unit != "kb" && unit != "mb" && unit != "gb")
                throw new FormatException("Unsupported size unit: " + parts[1]);
            string path = parts[3].Trim();
            if (path.Length == 0)
                throw new FormatException("Missing asset path");
            return new BuildLogTreeItem("BuildLogLine", 0, id, size, unit, percentage, path);
        }
    }
}