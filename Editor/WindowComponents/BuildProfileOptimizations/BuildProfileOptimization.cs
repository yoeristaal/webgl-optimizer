using System;
using System.Collections.Generic;
using System.Linq;
using CrazyGames.TreeLib;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CrazyGames.WindowComponents.BuildProfileOptimizations
{
    public class BuildProfileOptimization : EditorWindow
    {
        private static MultiColumnHeaderState _multiColumnHeaderState;
        private static BuildProfileTree _buildProfileTree;

        private static readonly AnalysisJob _analysis = new AnalysisJob();
        private static bool _hasAnalyzed;

        public static void RenderGUI()
        {
            if (!_hasAnalyzed) AnalyzeBuildProfiles();
            var rect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(300));
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Build profiles are analyzed automatically when this tab first opens.");
            GUILayout.Label("Use Analyze build profiles to refresh the data.");
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            _buildProfileTree?.OnGUI(rect);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_analysis.IsRunning ? "Cancel analysis" : "Analyze build profiles", GUILayout.Width(200)))
            {
                if (_analysis.IsRunning)
                    _analysis.Cancel();
                else
                    AnalyzeBuildProfiles();
            }
            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
            _analysis.RenderStatus();
            EditorGUILayout.Space(5);

            GUILayout.Label(
                "This utility gives you an overview of all Build Profiles in your project. You can see which profiles are configured, their target platforms, and key settings that affect build size and performance.",
                EditorStyles.wordWrappedLabel);

            BuildExplanation("Profile Name", "The full name of the build profile as configured in your project.");
            BuildExplanation("Platform", "The platform/host for this build (e.g., Web, Poki, LOL, Win, Mac).");
            BuildExplanation("Game Name", "The name of the game extracted from the profile name.");
            BuildExplanation("Active", "Whether this profile is currently set as the active build profile.");
            BuildExplanation("Scenes", "Number of scenes included in this build profile.");
        }

        static void BuildExplanation(string label, string explanation)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, EditorStyles.boldLabel, GUILayout.Width(130));
            GUILayout.Label(
                explanation,
                EditorStyles.wordWrappedLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        internal static void CancelAnalysis() => _analysis.Cancel();

        static void AnalyzeBuildProfiles()
        {
            _hasAnalyzed = true;
            _analysis.Start(AnalyzeBuildProfilesSteps);
        }

        static IEnumerator<string> AnalyzeBuildProfilesSteps()
        {
            var treeElements = new List<BuildProfileTreeItem>();
            var idIncrement = 0;
            var root = new BuildProfileTreeItem("Root", -1, idIncrement, null);
            treeElements.Add(root);

            yield return "Finding build profiles...";
            var profileGuids = AssetDatabase.FindAssets("t:BuildProfile");
            foreach (var guid in profileGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                yield return "Analyzing profile: " + path;
                try
                {
                    idIncrement++;
                    var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
                    if (profile == null)
                        throw new InvalidOperationException("Build profile could not be loaded.");
                    treeElements.Add(new BuildProfileTreeItem("BuildProfile", 0, idIncrement, profile));
                }
                catch (Exception exception) { _analysis.ReportSkipped(path, exception); }
            }

            yield return "Building results table...";

            var treeModel = new TreeModel<BuildProfileTreeItem>(treeElements);
            var treeViewState = new TreeViewState<int>();

            if (_multiColumnHeaderState == null)
                _multiColumnHeaderState = new MultiColumnHeaderState(new[]
                {
                    new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Profile Name"}, width = 200, minWidth = 150, canSort = true},
                    new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Platform"}, width = 100, minWidth = 80, canSort = true},
                    new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Game Name"}, width = 120, minWidth = 100, canSort = true},
                    new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "First Scene"}, width = 80, minWidth = 60, canSort = true},
                    new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Active"}, width = 60, minWidth = 60, canSort = true},
                    new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Scenes"}, width = 80, minWidth = 60, canSort = true},
                });

            _buildProfileTree = new BuildProfileTree(treeViewState, new MultiColumnHeader(_multiColumnHeaderState), treeModel);

            if (OptimizerWindow.EditorWindowInstance != null)
            {
                OptimizerWindow.EditorWindowInstance.Repaint();
            }
        }
    }
}
