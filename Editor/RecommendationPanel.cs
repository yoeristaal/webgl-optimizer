using System;
using System.Collections.Generic;
using System.Linq;
using CrazyGames.TreeLib;
using UnityEditor;
using UnityEngine;

namespace CrazyGames
{
    internal sealed class RecommendationPanel
    {
        private static readonly AnalysisJob Changes = new AnalysisJob("Batch complete.",
            "Remaining changes cancelled. Applied fixes can be restored.", "Completed changes can be restored.");
        private static OptimizationHistoryStore _history;
        private static int _revision;
        private readonly AnalysisJob _scan = new AnalysisJob();
        private readonly OptimizationAssetKind _kind;
        private List<OptimizationRecommendation> _issues = new List<OptimizationRecommendation>();
        private OptimizationRecommendation[] _preview;
        private Vector2 _scroll;
        private bool _expanded = true;
        private int _seenRevision;
        private object _lastTree;
        private int _page;
        private bool _refreshRequested;
        private string _contextKey;

        public RecommendationPanel(OptimizationAssetKind kind) { _kind = kind; }

        public void Render<T>(TreeViewWithTreeModel<T> tree, Func<T, string> assetPath,
            bool analysisBusy, Action refreshTable) where T : TreeElement
        {
            var context = ActivePlatform.Current;
            if (_contextKey != context.Key)
            {
                _contextKey = context.Key;
                _scan.Cancel();
                _issues.Clear();
                _preview = null;
                _page = 0;
            }
            if (!ReferenceEquals(_lastTree, tree))
            {
                _lastTree = tree;
                _scan.Cancel();
                _issues.Clear();
                _preview = null;
                _page = 0;
            }
            if (_seenRevision != _revision)
            {
                _seenRevision = _revision;
                _scan.Cancel();
                _issues.Clear();
                _preview = null;
                _refreshRequested = tree != null;
            }
            if (_refreshRequested && !analysisBusy && !Changes.IsRunning)
            {
                _refreshRequested = false;
                refreshTable();
                analysisBusy = true;
            }
            _expanded = EditorGUILayout.Foldout(_expanded, "Recommendations and batch fixes", true);
            if (!_expanded)
                return;

            bool historyReady = true;
            try { if (_history == null) _history = new OptimizationHistoryStore(); }
            catch (Exception exception)
            {
                historyReady = false;
                EditorGUILayout.HelpBox("Restore history could not be loaded: " + exception.Message, MessageType.Error);
            }
            bool busy = analysisBusy || _scan.IsRunning || Changes.IsRunning;
            using (new EditorGUI.DisabledScope(busy || tree == null))
            {
                if (GUILayout.Button("Find / refresh recommendations"))
                {
                    var paths = tree.treeModel.root.children == null ? new string[0] :
                        tree.treeModel.root.children.OfType<T>().Select(assetPath).Where(path => !string.IsNullOrEmpty(path)).ToArray();
                    _preview = null;
                    _scan.Start(() => Scan(paths, context));
                }
            }
            if (_scan.IsRunning && GUILayout.Button("Cancel recommendation scan"))
                _scan.Cancel();
            _scan.RenderStatus();

            using (new EditorGUI.DisabledScope(busy || _preview != null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Check recommendations for selected assets"))
                {
                    var paths = new HashSet<string>();
                    if (tree != null)
                        foreach (int id in tree.GetSelection())
                        {
                            var item = tree.treeModel.Find(id);
                            if (item != null) paths.Add(assetPath(item));
                        }
                    foreach (var issue in _issues) issue.Selected = paths.Contains(issue.Path);
                }
                if (GUILayout.Button("Clear checks"))
                    foreach (var issue in _issues) issue.Selected = false;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.LabelField($"{_issues.Count} recommendation(s), ranked by severity. Check only changes suitable for your game.", EditorStyles.wordWrappedLabel);
            IList<OptimizationRecommendation> visible = _preview != null ? (IList<OptimizationRecommendation>)_preview : _issues;
            int pageCount = Math.Max(1, (visible.Count + 49) / 50);
            _page = Math.Min(_page, pageCount - 1);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_page == 0))
                if (GUILayout.Button("Previous page")) { _page--; _scroll = Vector2.zero; }
            EditorGUILayout.LabelField($"Page {_page + 1} / {pageCount}");
            using (new EditorGUI.DisabledScope(_page >= pageCount - 1))
                if (GUILayout.Button("Next page")) { _page++; _scroll = Vector2.zero; }
            EditorGUILayout.EndHorizontal();
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(230));
            using (new EditorGUI.DisabledScope(busy || _preview != null))
                foreach (var issue in visible.Skip(_page * 50).Take(50))
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    issue.Selected = EditorGUILayout.ToggleLeft($"{issue.Severity}: {issue.Title}", issue.Selected, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(issue.Path, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField(issue.Change, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField(issue.Explanation, EditorStyles.wordWrappedLabel);
                    if (GUILayout.Button("Select asset", GUILayout.Width(100)))
                        Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(issue.Path);
                    EditorGUILayout.EndVertical();
                }
            EditorGUILayout.EndScrollView();

            if (_preview == null)
            {
                using (new EditorGUI.DisabledScope(busy || !historyReady || !_issues.Any(issue => issue.Selected)))
                    if (GUILayout.Button("Preview checked fixes"))
                    {
                        _preview = _issues.Where(issue => issue.Selected).ToArray();
                        _page = 0;
                        _scroll = Vector2.zero;
                    }
            }
            else
            {
                int assets = _preview.Select(issue => issue.Path).Distinct().Count();
                EditorGUILayout.HelpBox($"Preview: {_preview.Length} change(s) across {assets} asset(s). Each asset will be reimported. Texture and audio changes create or update the {context.DisplayName} override; model changes affect all platforms. Previous importer settings will be saved for Restore last batch.", MessageType.Info);
                using (new EditorGUI.DisabledScope(busy || !historyReady))
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Apply previewed fixes"))
                    {
                        var selected = _preview;
                        _preview = null;
                        _scan.Cancel();
                        Changes.Start(() => RunChanges(OptimizationBatch.Apply(selected,
                            path => new ImporterOptimization(path, context.ImporterPlatform, context.Key), _history, Changes.ReportSkipped)));
                    }
                    if (GUILayout.Button("Back to recommendations")) _preview = null;
                    EditorGUILayout.EndHorizontal();
                }
            }

            using (new EditorGUI.DisabledScope(busy || !historyReady || _history == null || _history.Backups.Count == 0))
                if (GUILayout.Button("Restore last batch"))
                {
                    _scan.Cancel();
                    _preview = null;
                    Changes.Start(() => RunChanges(OptimizationBatch.RestoreLast(
                        OptimizationHistoryStore.ResolveBackup, _history, Changes.ReportSkipped)));
                }
            if (Changes.IsRunning && GUILayout.Button("Cancel remaining changes")) Changes.Cancel();
            Changes.RenderStatus();
        }

        private IEnumerator<string> Scan(string[] paths, PlatformContext context)
        {
            var issues = new List<OptimizationRecommendation>();
            foreach (string path in paths)
            {
                yield return "Reviewing: " + path;
                if (ActivePlatform.Current.Key != context.Key)
                    throw new InvalidOperationException("Active build profile changed. Refresh recommendations.");
                // Packages can be displayed by analysis, but are never offered for modification.
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                try
                {
                    var importer = new ImporterOptimization(path, context.ImporterPlatform, context.Key);
                    string baseline = importer.Capture();
                    var recommendations = OptimizationRules.Evaluate(importer.GetFacts(_kind));
                    foreach (var issue in recommendations)
                    {
                        issue.Guid = importer.Guid;
                        issue.Baseline = baseline;
                    }
                    issues.AddRange(recommendations);
                }
                catch (Exception exception) { _scan.ReportSkipped(path, exception); }
            }
            if (ActivePlatform.Current.Key == context.Key)
                _issues = OptimizationRules.Rank(issues);
        }

        private static IEnumerator<string> RunChanges(IEnumerable<string> steps)
        {
            try
            {
                foreach (string progress in steps) yield return progress;
            }
            finally { _revision++; }
        }

        public void CancelScan() => _scan.Cancel();
        public static void CancelChanges() => Changes.Cancel();
    }
}
