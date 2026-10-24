using CrazyGames.TreeLib;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CrazyGames.WindowComponents.MaterialOptimizations
{
    internal static class MaterialOptimization
    {
        private static readonly AnalysisJob _analysis = new AnalysisJob();
        private static MaterialTree _tree;
        private static MultiColumnHeaderState _headerState;
        private static bool _hasAnalyzed;
        private static bool _includePackages;
        private static string _contextKey;
        private static string _search = "";
        private static int _materialCount;

        public static void RenderGUI()
        {
            var context = ActivePlatform.Current;
            if (_contextKey != context.Key)
            {
                bool hadResults = _tree != null || _analysis.IsRunning;
                _contextKey = context.Key;
                CancelAnalysis();
                _tree = null;
                if (hadResults) AnalyzeMaterials();
            }
            if (!_hasAnalyzed) AnalyzeMaterials();
            ActivePlatform.RenderContext(false);
            _search = EditorGUILayout.TextField("Search materials", _search);
            var rect = GUILayoutUtility.GetRect(0, 300, GUILayout.ExpandWidth(true));
            if (_tree != null)
            {
                _tree.searchString = _materialCount == 0 ? "" : _search;
                _tree.OnGUI(rect);
            }
            else GUI.Label(rect, "Materials are analyzed automatically when this tab first opens.", EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_analysis.IsRunning ? "Cancel analysis" : "Analyze materials", GUILayout.Width(200)))
            {
                if (_analysis.IsRunning) CancelAnalysis();
                else AnalyzeMaterials();
            }
            using (new EditorGUI.DisabledScope(_analysis.IsRunning))
            {
                bool includePackages = GUILayout.Toggle(_includePackages, "Include files from Packages");
                if (includePackages != _includePackages)
                {
                    _includePackages = includePackages;
                    AnalyzeMaterials();
                }
            }
            EditorGUILayout.EndHorizontal();
            _analysis.RenderStatus();
            if (_tree != null) EditorGUILayout.LabelField($"{_materialCount} material(s) found.");
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Materials are collected from the active build profile's scenes and Resources dependencies, including embedded model materials. " +
                "Select a row to inspect the material; double-click to ping it. Use Analyze materials to refresh after edits or reimports. " +
                "Materials loaded only through other systems, such as Addressables, may be absent.", MessageType.Info);
            Explain("Render queue", "Effective queue, including the shader default. Transparent queues can increase overdraw; the queue alone does not describe a shader's blending behavior.");
            Explain("Shader passes", "Passes exposed by the material's shader. This is not the number of draw calls; the active render pipeline determines which passes execute.");
            Explain("GPU instancing", "Whether the material enables instancing. Actual batching depends on shader, renderer, and pipeline support; enabling it is not always beneficial.");
            Explain("Keywords", "Enabled material shader keyword count; hover for the list. Keywords can contribute to shader variants, but this count is not a variant count.");
            Explain("Textures / memory", "Unique assigned textures, counted once per material. Memory is their native memory reported by the Editor profiler, not compressed build size or a player GPU estimate. Shared textures appear in each material; do not sum rows. N/A means memory is unavailable.");
            Explain("Double-sided GI", "Whether lightmapping treats both sides as contributing to global illumination. This does not control rendering culling.");
        }

        private static void Explain(string label, string explanation)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, EditorStyles.boldLabel, GUILayout.Width(135));
            GUILayout.Label(explanation, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndHorizontal();
        }

        internal static void CancelAnalysis() => _analysis.Cancel();

        private static void AnalyzeMaterials()
        {
            _hasAnalyzed = true;
            bool includePackages = _includePackages;
            string contextKey = ActivePlatform.Current.Key;
            _analysis.Start(() => AnalyzeSteps(includePackages, contextKey));
        }

        private static IEnumerator<string> AnalyzeSteps(bool includePackages, string contextKey)
        {
            var paths = new HashSet<string>();
            foreach (string progress in AssetAnalysis.CollectPaths(
                path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(Material) || AssetImporter.GetAtPath(path) is ModelImporter,
                paths, _analysis))
                yield return progress;

            var elements = new List<MaterialTreeItem> { new MaterialTreeItem(0, null, null) };
            var seen = new HashSet<Material>();
            int id = 0;
            foreach (string path in paths)
            {
                yield return "Loading materials: " + path;
                if (!includePackages && path.StartsWith("Packages/", StringComparison.Ordinal)) continue;
                UnityEngine.Object[] assets;
                try { assets = AssetDatabase.LoadAllAssetsAtPath(path); }
                catch (Exception exception)
                {
                    _analysis.ReportSkipped(path, exception);
                    continue;
                }
                foreach (var asset in assets)
                {
                    if (!(asset is Material material) || !seen.Add(material)) continue;
                    yield return "Analyzing material: " + path + " / " + material.name;
                    try { elements.Add(new MaterialTreeItem(++id, material, path)); }
                    catch (Exception exception) { _analysis.ReportSkipped(path, exception); }
                }
            }

            yield return "Building materials table...";
            if (contextKey != ActivePlatform.Current.Key) yield break;
            if (_headerState == null)
                _headerState = new MultiColumnHeaderState(new[]
                {
                    Column("Material", "Material name; hover for the asset path.", 170),
                    Column("Shader", "Shader assigned to the material.", 200),
                    Column("Render queue", "Effective rendering order, including the shader default.", 95),
                    Column("Shader passes", "Shader pass count, not draw calls.", 95),
                    Column("GPU instancing", "Material instancing flag, not a guarantee of batching.", 105),
                    Column("Keywords", "Enabled shader keywords; hover a value to see the list.", 80),
                    Column("Textures", "Unique assigned textures per material.", 75),
                    Column("Texture memory", "Editor native texture memory; shared textures repeat across rows. Not build size.", 115),
                    Column("Double-sided GI", "Double-sided contribution to baked global illumination.", 110),
                    Column("Asset path", "Asset containing the material, including model sub-assets.", 250)
                });
            var tree = new MaterialTree(new TreeViewState<int>(), new MultiColumnHeader(_headerState),
                new TreeModel<MaterialTreeItem>(elements));
            _tree = tree;
            _materialCount = elements.Count - 1;
            OptimizerWindow.EditorWindowInstance?.Repaint();
        }

        private static MultiColumnHeaderState.Column Column(string title, string tooltip, float width)
        {
            return new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent(title, tooltip), width = width, minWidth = width, canSort = true
            };
        }
    }
}
