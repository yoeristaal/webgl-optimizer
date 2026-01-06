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

        private static bool _isAnalyzing;

        public static void RenderGUI()
        {
            var rect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(300));
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Press \"Analyze build profiles\" button to load the table.");
            GUILayout.Label("Press it again when you need to refresh the data.");
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            _buildProfileTree?.OnGUI(rect);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_isAnalyzing ? "Analyzing..." : "Analyze build profiles", GUILayout.Width(200)))
            {
                AnalyzeBuildProfiles();
            }
            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
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

        static void AnalyzeBuildProfiles()
        {
            Debug.Log("AnalyzeBuildProfiles button clicked - Starting analysis...");

            _isAnalyzing = true;
            if (OptimizerWindow.EditorWindowInstance != null)
            {
                OptimizerWindow.EditorWindowInstance.Repaint();
            }

            var treeElements = new List<BuildProfileTreeItem>();
            var idIncrement = 0;
            var root = new BuildProfileTreeItem("Root", -1, idIncrement, null);
            treeElements.Add(root);

            try
            {
                // Get all build profiles in the project
                var profileGuids = AssetDatabase.FindAssets("t:BuildProfile");
                Debug.Log($"Found {profileGuids.Length} build profiles in project");

                foreach (var guid in profileGuids)
                {
                    idIncrement++;
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);

                    if (profile != null)
                    {
                        treeElements.Add(new BuildProfileTreeItem("BuildProfile", 0, idIncrement, profile));
                        Debug.Log($"Added build profile: {profile.name}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to analyze build profiles. Error: {e.Message}");
            }

            Debug.Log($"Created tree with {treeElements.Count} elements (including root)");

            var treeModel = new TreeModel<BuildProfileTreeItem>(treeElements);
            var treeViewState = new TreeViewState();

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
            _isAnalyzing = false;

            Debug.Log("Analysis complete!");

            if (OptimizerWindow.EditorWindowInstance != null)
            {
                OptimizerWindow.EditorWindowInstance.Repaint();
            }
        }
    }
}