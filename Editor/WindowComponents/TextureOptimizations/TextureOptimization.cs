using System;
using System.Collections.Generic;
using CrazyGames.TreeLib;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CrazyGames.WindowComponents.TextureOptimizations
{
    public class TextureOptimization : EditorWindow
    {
        private static MultiColumnHeaderState _multiColumnHeaderState;
        private static TextureTree _textureCompressionTree;

        private static readonly AnalysisJob _analysis = new AnalysisJob();
        private static readonly RecommendationPanel _recommendations = new RecommendationPanel(OptimizationAssetKind.Texture);
        private static bool _includeFilesFromPackages;
        private static string _contextKey;
        private static int _settingsRevision = -1;

        public static void RenderGUI()
        {
            var context = ActivePlatform.Current;
            if (_contextKey != context.Key)
            {
                bool hadResults = _textureCompressionTree != null || _analysis.IsRunning;
                _contextKey = context.Key;
                CancelAnalysis();
                _textureCompressionTree = null;
                if (hadResults) AnalyzeTextures();
            }
            if (_settingsRevision != ActivePlatform.Revision)
            {
                _settingsRevision = ActivePlatform.Revision;
                _textureCompressionTree?.Reload();
            }
            ActivePlatform.RenderContext();
            var rect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(300));
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Press \"Analyze textures\" button to load the table.");
            GUILayout.Label("Press it again when you need to refresh the data.");
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            _textureCompressionTree?.OnGUI(rect);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_analysis.IsRunning ? "Cancel analysis" : "Analyze textures", GUILayout.Width(200)))
            {
                if (_analysis.IsRunning)
                    _analysis.Cancel();
                else
                    AnalyzeTextures();
            }

            var originalValue = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 160;
            using (new EditorGUI.DisabledScope(_analysis.IsRunning))
                _includeFilesFromPackages = EditorGUILayout.Toggle("Include files from Packages", _includeFilesFromPackages);
            EditorGUIUtility.labelWidth = originalValue;
            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
            _analysis.RenderStatus();
            _recommendations.Render(_textureCompressionTree, item => item.TexturePath, _analysis.IsRunning, AnalyzeTextures);
            EditorGUILayout.Space(5);

            GUILayout.Label(
                "This utility gives you an overview of the textures used in your project. By optimizing various settings, you will be able to considerably decrease your final build size. You can click on a texture to select it in the Project view. To find out more about how the tool finds the textures, please check our GitHub repo.",
                EditorStyles.wordWrappedLabel);


            BuildExplanation("Max size",
                "Decrease the max size as much as possible while the texture still looks good in game. You most likely don't need the default 2048 set by Unity.");
            BuildExplanation("Compression", "Lower quality will decrease the final build size.");
            BuildExplanation("Crunch compression",
                "All the textures with crunch compression enabled will be compressed together, decreasing the final build size.");
            BuildExplanation("Crunch comp. quality", "A higher compression quality means larger textures and longer compression times.");
            BuildExplanation("Settings source", "Default means no active platform override is enabled. Otherwise, the table shows that platform's override. Desktop overrides are shared by Windows, macOS and Linux.");
            BuildExplanation("Format", "An explicit format controls texture compression; the Compression and Crunch fields are used with Automatic formats where supported.");
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

        internal static void CancelAnalysis()
        {
            _analysis.Cancel();
            _recommendations.CancelScan();
        }

        static void AnalyzeTextures()
        {
            bool includePackages = _includeFilesFromPackages;
            string contextKey = ActivePlatform.Current.Key;
            _analysis.Start(() => AnalyzeTexturesSteps(includePackages, contextKey));
        }

        static IEnumerator<string> AnalyzeTexturesSteps(bool includePackages, string contextKey)
        {
            var paths = new HashSet<string>();
            foreach (string progress in AssetAnalysis.CollectPaths(path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(Texture2D), paths, _analysis))
                yield return progress;

            var treeElements = new List<TextureTreeItem>();
            treeElements.Add(new TextureTreeItem("Root", -1, 0, null, null));
            int idIncrement = 0;
            foreach (string texturePath in paths)
            {
                yield return "Analyzing: " + texturePath;
                if (texturePath.StartsWith("Packages/", StringComparison.Ordinal) && !includePackages)
                    continue;
                try
                {
                    var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                    if (importer == null)
                        throw new InvalidOperationException("Asset has no compatible importer.");
                    treeElements.Add(new TextureTreeItem("Texture2D", 0, ++idIncrement, texturePath, importer));
                }
                catch (Exception exception) { _analysis.ReportSkipped(texturePath, exception); }
            }

            yield return "Building results table...";
            if (contextKey != ActivePlatform.Current.Key) yield break;
            var treeModel = new TreeModel<TextureTreeItem>(treeElements);
            var treeViewState = new TreeViewState<int>();
            if (_multiColumnHeaderState == null)
                _multiColumnHeaderState = new MultiColumnHeaderState(new[]
                {
                    // when adding a new column don't forget to check the sorting method, and the CellGUI method
                    new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Texture"}, width = 150, minWidth = 150, canSort = true},
                    new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Type"}, width = 60, minWidth = 60, canSort = true},
                    new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Max size"}, width = 60, minWidth = 60, canSort = true},
                    new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Compression"}, width = 80, minWidth = 80, canSort = true},
                    new MultiColumnHeaderState.Column()
                        {headerContent = new GUIContent() {text = "Crunch compression"}, width = 120, minWidth = 120, canSort = true},
                    new MultiColumnHeaderState.Column()
                        {headerContent = new GUIContent() {text = "Crunch comp. quality"}, width = 128, minWidth = 128, canSort = true},
                    new MultiColumnHeaderState.Column()
                        {headerContent = new GUIContent() {text = "Settings source"}, width = 130, minWidth = 130, canSort = true},
                    new MultiColumnHeaderState.Column()
                        {headerContent = new GUIContent() {text = "Format"}, width = 110, minWidth = 110, canSort = true},
                });
            _textureCompressionTree = new TextureTree(treeViewState, new MultiColumnHeader(_multiColumnHeaderState), treeModel);

            if (OptimizerWindow.EditorWindowInstance != null)
            {
                OptimizerWindow.EditorWindowInstance.Repaint();
            }
        }
    }
}
