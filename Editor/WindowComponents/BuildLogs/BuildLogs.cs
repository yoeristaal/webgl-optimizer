using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private static bool _hasAnalyzed;

        public static void RenderGUI()
        {
            if (!_hasAnalyzed) AnalyzeBuildLogs();
            var rect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(300));
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Build logs are analyzed automatically when this tab first opens. Build the project at least once on this machine.");
            GUILayout.Label("Use Analyze build logs to refresh the data.");
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
                "This utility analyzes the Build Report from the Editor.log file. It displays asset sizes from the latest complete report, before build/download compression. Asset share is calculated from all listed assets, including hidden packages; it excludes build overhead. You can use this utility to detect more opportunities to decrease the final build size. There may be textures that still occupy a lot of memory, uncompressed sounds, or stuff forgotten in the Resources folders that gets included in the build.",
                EditorStyles.wordWrappedLabel);
        }

        private static string GetEditorLogPath() => Application.consoleLogPath;

        internal static void CancelAnalysis() => _analysis.Cancel();

        static void AnalyzeBuildLogs()
        {
            _hasAnalyzed = true;
            bool includePackages = _includeFilesFromPackages;
            _analysis.Start(() => AnalyzeBuildLogsSteps(includePackages));
        }

        static IEnumerator<string> AnalyzeBuildLogsSteps(bool includePackages)
        {
            yield return "Reading the latest build report...";
            var report = new BuildAssetReport();
            foreach (string progress in report.Read(GetEditorLogPath(), _analysis, true))
                yield return progress;
            var treeElements = new List<BuildLogTreeItem>();
            treeElements.Add(new BuildLogTreeItem("Root", -1, 0, 0, "", 0, ""));
            foreach (var entry in report.Entries)
            {
                if (entry.filePath.StartsWith("Packages/", StringComparison.Ordinal) && !includePackages)
                    continue;
                treeElements.Add(entry);
            }

            yield return "Building results table...";
            var treeModel = new TreeModel<BuildLogTreeItem>(treeElements);
            var treeViewState = new TreeViewState<int>();
            _multiColumnHeaderState = _multiColumnHeaderState ?? new MultiColumnHeaderState(new[]
            {
                // when adding a new column don't forget to check the sorting method, and the CellGUI method
                new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Size"}, width = 80, minWidth = 60, canSort = true},
                new MultiColumnHeaderState.Column() {headerContent = new GUIContent("Asset share %", "Share of all listed asset bytes, including hidden package assets. Excludes build overhead and download compression."), width = 110, minWidth = 100, canSort = true},
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
            return BuildAssetReport.ParseAssetLine(line, id);
        }
    }
}
