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
        private bool _scanAfterRefresh;
        private bool _showDetails;
        private OptimizationRule? _detailRule;

        public RecommendationPanel(OptimizationAssetKind kind) { _kind = kind; }

        public void Render<T>(TreeViewWithTreeModel<T> tree, Func<T, string> assetPath,
            bool analysisBusy, Action refreshTable) where T : TreeElement
        {
            var context = ActivePlatform.Current;
            if (_contextKey != context.Key)
            {
                _contextKey = context.Key;
                _scanAfterRefresh = false;
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
                _scanAfterRefresh = true;
            }
            if (_refreshRequested && !analysisBusy && !Changes.IsRunning)
            {
                _refreshRequested = false;
                refreshTable();
                analysisBusy = true;
            }
            if (_scanAfterRefresh && !_refreshRequested && !analysisBusy && !Changes.IsRunning && tree != null)
            {
                _scanAfterRefresh = false;
                StartScan(tree, assetPath, context);
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
                    StartScan(tree, assetPath, context);
                }
            }
            if (_scan.IsRunning && GUILayout.Button("Cancel recommendation scan"))
                _scan.Cancel();
            _scan.RenderStatus();

            if (_preview != null)
            {
                int assets = _preview.Select(issue => issue.Path).Distinct().Count();
                EditorGUILayout.HelpBox($"Ready to execute {_preview.Length} fix(es) across {assets} asset(s). Texture and audio fixes target the {context.DisplayName} override; model fixes affect all platforms. Importer settings are backed up for Restore last batch. Analysis and recommendations refresh automatically afterwards.", MessageType.Info);
                using (new EditorGUI.DisabledScope(busy || !historyReady))
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button($"Execute fixes on {assets} asset(s)"))
                    {
                        var selected = _preview;
                        _preview = null;
                        _scan.Cancel();
                        Changes.Start(() => RunChanges(OptimizationBatch.Apply(selected,
                            path => new ImporterOptimization(path, context.ImporterPlatform, context.Key), _history, Changes.ReportSkipped)));
                    }
                    if (GUILayout.Button("Cancel preview")) _preview = null;
                    EditorGUILayout.EndHorizontal();
                }
                busy |= Changes.IsRunning;
            }
            else
            {
                var selectedPaths = new HashSet<string>();
                if (tree != null)
                    foreach (int id in tree.GetSelection())
                    {
                        var item = tree.treeModel.Find(id);
                        if (item != null) selectedPaths.Add(assetPath(item));
                    }
                var selectedIssues = _issues.Where(issue => selectedPaths.Contains(issue.Path)).ToArray();
                using (new EditorGUI.DisabledScope(busy || !historyReady || selectedIssues.Length == 0))
                    if (GUILayout.Button($"Preview fixes for selected assets ({selectedIssues.Select(issue => issue.Path).Distinct().Count()})"))
                        SetPreview(selectedIssues);
                using (new EditorGUI.DisabledScope(busy || !historyReady || !_issues.Any(issue => issue.Selected)))
                    if (GUILayout.Button("Preview checked individual fixes"))
                        SetPreview(_issues.Where(issue => issue.Selected));
            }

            var visible = _preview ?? _issues.ToArray();
            var groups = OptimizationRules.Summarize(visible);
            int assetCount = visible.Select(issue => issue.Path).Distinct().Count();
            EditorGUILayout.LabelField($"{assetCount} asset(s) • {groups.Count} fix type(s) • {visible.Length} recommendation(s)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Choose a fix group below to preview and execute it for every relevant asset. An asset may appear in more than one group.", EditorStyles.wordWrappedLabel);
            foreach (var group in groups)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{group.AssetCount} asset(s): {group.Title}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(group.ChangeSummary, EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(group.Issues[0].Explanation, EditorStyles.wordWrappedLabel);
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(busy || !historyReady || _preview != null))
                    if (GUILayout.Button($"Preview batch fix for all {group.AssetCount} asset(s)"))
                        SetPreview(group.Issues);
                if (GUILayout.Button("View affected files"))
                {
                    _detailRule = group.Rule;
                    _showDetails = true;
                    _page = 0;
                    _scroll = Vector2.zero;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            _showDetails = EditorGUILayout.Foldout(_showDetails, "Individual fixes (optional)", true);
            if (_showDetails)
            {
                if (_detailRule.HasValue && GUILayout.Button("Show files for all fix types")) { _detailRule = null; _page = 0; }
                var details = visible.Where(issue => !_detailRule.HasValue || issue.Rule == _detailRule.Value).ToArray();
                int pageCount = Math.Max(1, (details.Length + 49) / 50);
                _page = Math.Min(_page, pageCount - 1);
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(_page == 0))
                    if (GUILayout.Button("Previous page")) { _page--; _scroll = Vector2.zero; }
                EditorGUILayout.LabelField($"Page {_page + 1} / {pageCount}");
                using (new EditorGUI.DisabledScope(_page >= pageCount - 1))
                    if (GUILayout.Button("Next page")) { _page++; _scroll = Vector2.zero; }
                EditorGUILayout.EndHorizontal();
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(230));
                foreach (var issue in details.Skip(_page * 50).Take(50))
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    using (new EditorGUI.DisabledScope(busy || _preview != null))
                        issue.Selected = EditorGUILayout.ToggleLeft(issue.Title, issue.Selected, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(issue.Path, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField(issue.Change, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Select asset")) Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(issue.Path);
                    using (new EditorGUI.DisabledScope(busy || !historyReady || _preview != null))
                        if (GUILayout.Button("Preview this fix")) SetPreview(new[] { issue });
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndScrollView();
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

        private void SetPreview(IEnumerable<OptimizationRecommendation> issues)
        {
            _preview = issues.ToArray();
            _page = 0;
            _scroll = Vector2.zero;
        }

        private void StartScan<T>(TreeViewWithTreeModel<T> tree, Func<T, string> assetPath, PlatformContext context) where T : TreeElement
        {
            var paths = tree.treeModel.root.children == null ? new string[0] :
                tree.treeModel.root.children.OfType<T>().Select(assetPath).Where(path => !string.IsNullOrEmpty(path)).Distinct().ToArray();
            _preview = null;
            _issues.Clear();
            _scan.Start(() => Scan(paths, context));
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
