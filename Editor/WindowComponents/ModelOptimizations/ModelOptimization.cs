using CrazyGames.TreeLib;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CrazyGames.WindowComponents.ModelOptimizations
{
    public class ModelOptimization : EditorWindow
    {
        private static MultiColumnHeaderState _multiColumnHeaderState;
        private static ModelTree _modelTree;

        private static readonly AnalysisJob _analysis = new AnalysisJob();
        private static readonly RecommendationPanel _recommendations = new RecommendationPanel(OptimizationAssetKind.Model);
        private static bool _includeFilesFromPackages;
        private static string _contextKey;

        private static readonly List<string> _modelFormats = new List<string>() { ".fbx", ".dae", ".3ds", ".dxf", ".obj" };

        public static void RenderGUI()
        {
            var context = ActivePlatform.Current;
            if (_contextKey != context.Key)
            {
                bool hadResults = _modelTree != null || _analysis.IsRunning;
                _contextKey = context.Key;
                CancelAnalysis();
                _modelTree = null;
                if (hadResults) AnalyzeModels();
            }
            ActivePlatform.RenderContext(false);
            EditorGUILayout.LabelField("Model import settings apply to all platforms.", EditorStyles.wordWrappedLabel);
            var rect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(300));
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Press \"Analyze models\" button to load the table.");
            GUILayout.Label("Press it again when you need to refresh the data.");
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            _modelTree?.OnGUI(rect);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(_analysis.IsRunning ? "Cancel analysis" : "Analyze models", GUILayout.Width(200)))
            {
                if (_analysis.IsRunning)
                    _analysis.Cancel();
                else
                    AnalyzeModels();
            }

            var originalValue = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 160;
            using (new EditorGUI.DisabledScope(_analysis.IsRunning))
                _includeFilesFromPackages = EditorGUILayout.Toggle("Include files from Packages", _includeFilesFromPackages);
            EditorGUIUtility.labelWidth = originalValue;
            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
            _analysis.RenderStatus();
            _recommendations.Render(_modelTree, item => item.ModelPath, _analysis.IsRunning, AnalyzeModels);
            EditorGUILayout.Space(5);

            GUILayout.Label(
                "This utility gives you an overview of the models used in your project. By optimizing various settings, you will be able to considerably decrease your final build size. You can click on a model to select it in the Project view. To find out more about how the tool finds the models, please check our GitHub repo.",
                EditorStyles.wordWrappedLabel);

            BuildExplanation("R/W enabled",
                "When a Mesh is read/write enabled, Unity uploads the Mesh data to GPU-addressable memory, but also keeps it in CPU-addressable memory. In most cases, you should disable this option to save runtime memory usage.");
            BuildExplanation("Polygons optimized",
                "Optimize the order of polygons in the mesh to make better use of the GPUs internal caches to improve rendering performance.");
            BuildExplanation("Vertices optimized",
                "Optimize the order of vertices in the mesh to make better use of the GPUs internal caches to improve rendering performance.");
            BuildExplanation("Mesh compression",
                "Compressing meshes will decrease the final build size, but more compression introduces more artifacts in vertex data.");
            BuildExplanation("Animation compression",
                "Compressing animations will decrease the final build size, but more compression introduces more artifacts in the animations.");
        }

        static void BuildExplanation(string label, string explanation)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, EditorStyles.boldLabel, GUILayout.Width(130));
            GUILayout.Label(explanation, EditorStyles.wordWrappedLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        static bool IsModelAtPath(string assetDependency)
        {
            return AssetDatabase.GetMainAssetTypeAtPath(assetDependency) == typeof(GameObject) &&
                   _modelFormats.Contains(System.IO.Path.GetExtension(assetDependency).ToLowerInvariant());
        }

        internal static void CancelAnalysis()
        {
            _analysis.Cancel();
            _recommendations.CancelScan();
        }

        static void AnalyzeModels()
        {
            bool includePackages = _includeFilesFromPackages;
            string contextKey = ActivePlatform.Current.Key;
            _analysis.Start(() => AnalyzeModelsSteps(includePackages, contextKey));
        }

        static IEnumerator<string> AnalyzeModelsSteps(bool includePackages, string contextKey)
        {
            var paths = new HashSet<string>();
            foreach (string progress in AssetAnalysis.CollectPaths(path => IsModelAtPath(path), paths, _analysis))
                yield return progress;

            var treeElements = new List<ModelTreeItem>();
            treeElements.Add(new ModelTreeItem("Root", -1, 0, null, null));
            int idIncrement = 0;
            foreach (string modelPath in paths)
            {
                yield return "Analyzing: " + modelPath;
                if (modelPath.StartsWith("Packages/", StringComparison.Ordinal) && !includePackages)
                    continue;
                try
                {
                    var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                    if (importer == null)
                        throw new InvalidOperationException("Asset has no compatible importer.");
                    treeElements.Add(new ModelTreeItem("Model", 0, ++idIncrement, modelPath, importer));
                }
                catch (Exception exception) { _analysis.ReportSkipped(modelPath, exception); }
            }

            yield return "Building results table...";
            if (contextKey != ActivePlatform.Current.Key) yield break;
            var treeModel = new TreeModel<ModelTreeItem>(treeElements);
            var treeViewState = new TreeViewState<int>();

            if (_multiColumnHeaderState == null)
                _multiColumnHeaderState = new MultiColumnHeaderState(new[]
                {
                    // when adding a new column don't forget to check the sorting method, and the CellGUI method
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent() { text = "Model" }, width = 150, minWidth = 150, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "R/W enabled" }, width = 80, minWidth = 80, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "Polygons optimized" }, width = 120, minWidth = 120, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "Vertices optimized" }, width = 120, minWidth = 120, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "Mesh compression" }, width = 120, minWidth = 120, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "Animation compression" }, width = 140, minWidth = 140, canSort = true },
                });

            _modelTree = new ModelTree(treeViewState, new MultiColumnHeader(_multiColumnHeaderState), treeModel);

            if (OptimizerWindow.EditorWindowInstance != null)
            {
                OptimizerWindow.EditorWindowInstance.Repaint();
            }
        }
    }
}
