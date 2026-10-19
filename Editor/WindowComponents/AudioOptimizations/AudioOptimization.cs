using System;
using System.Collections.Generic;
using CrazyGames.TreeLib;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CrazyGames.WindowComponents.AudioOptimizations
{
    public class AudioOptimization : EditorWindow
    {
        private static MultiColumnHeaderState _multiColumnHeaderState;
        private static AudioTree _audioCompressionTree;

        private static readonly AnalysisJob _analysis = new AnalysisJob();
        private static readonly RecommendationPanel _recommendations = new RecommendationPanel(OptimizationAssetKind.Audio);
        private static bool _includeFilesFromPackages;
        private static bool _hasAnalyzed;
        private static string _contextKey;
        private static int _settingsRevision = -1;

        public static void RenderGUI()
        {
            var context = ActivePlatform.Current;
            if (_contextKey != context.Key)
            {
                bool hadResults = _audioCompressionTree != null || _analysis.IsRunning;
                _contextKey = context.Key;
                CancelAnalysis();
                _audioCompressionTree = null;
                if (hadResults) AnalyzeAudio();
            }
            if (!_hasAnalyzed) AnalyzeAudio();
            if (_settingsRevision != ActivePlatform.Revision)
            {
                _settingsRevision = ActivePlatform.Revision;
                _audioCompressionTree?.Reload();
            }
            ActivePlatform.RenderContext();
            var rect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(300));
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Audio clips are analyzed automatically when this tab first opens.");
            GUILayout.Label("Use Analyze audio to refresh the data.");
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            _audioCompressionTree?.OnGUI(rect);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_analysis.IsRunning ? "Cancel analysis" : "Analyze audio", GUILayout.Width(200)))
            {
                if (_analysis.IsRunning)
                    _analysis.Cancel();
                else
                    AnalyzeAudio();
            }

            var originalValue = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 160;
            using (new EditorGUI.DisabledScope(_analysis.IsRunning))
                _includeFilesFromPackages = EditorGUILayout.Toggle("Include files from Packages", _includeFilesFromPackages);
            EditorGUIUtility.labelWidth = originalValue;
            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
            _analysis.RenderStatus();
            _recommendations.Render(_audioCompressionTree, item => item.AudioPath, _analysis.IsRunning, AnalyzeAudio);
            EditorGUILayout.Space(5);

            GUILayout.Label(
                "This utility gives you an overview of the audio clips used in your project. By optimizing various settings, you will be able to considerably decrease your final build size and runtime memory usage. You can click on an audio clip to select it in the Project view. To find out more about how the tool finds the audio clips, please check our GitHub repo.",
                EditorStyles.wordWrappedLabel);


            BuildExplanation("Load type",
                "The default option, Decompress On Load, is good for audio clips that require precision when played, for example, audio effects or dialogues. For background audio clips Compressed In Memory is recommended, since it reduces the runtime memory, though audio playback is less precise and may introduce latency.");
            BuildExplanation("Quality",
                "Lowering the quality will reduce the build size. You can experiment with a lower audio quality for background audio.");
            BuildExplanation("Settings source", "Default means no active platform override is enabled. Otherwise, the table shows that platform's override. Desktop overrides are shared by Windows, macOS and Linux.");
            BuildExplanation("Imported Size", "Size of the imported audio data for the active platform, as shown in Unity's audio Inspector.");
            BuildExplanation("Ratio", "Imported size divided by original size, shown as a percentage. Lower values mean a smaller imported file relative to the original. N/A means Unity's size data is unavailable.");
            BuildExplanation("Clip details", "Duration, channels and sample rate describe the imported clip. Force to Mono shows the import setting. Hover over a clip name to see its asset path.");
            BuildExplanation("Loading", "Preload Audio Data shows the active platform setting (N/A for Streaming, where it is ignored). Load in Background shows whether audio loads asynchronously. Right-click a column header to show or hide columns.");
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

        static void AnalyzeAudio()
        {
            _hasAnalyzed = true;
            bool includePackages = _includeFilesFromPackages;
            string contextKey = ActivePlatform.Current.Key;
            _analysis.Start(() => AnalyzeAudioSteps(includePackages, contextKey));
        }

        static IEnumerator<string> AnalyzeAudioSteps(bool includePackages, string contextKey)
        {
            var paths = new HashSet<string>();
            foreach (string progress in AssetAnalysis.CollectPaths(path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(AudioClip), paths, _analysis))
                yield return progress;

            var treeElements = new List<AudioTreeItem>();
            treeElements.Add(new AudioTreeItem("Root", -1, 0, null, null));
            int idIncrement = 0;
            foreach (string audioPath in paths)
            {
                yield return "Analyzing: " + audioPath;
                if (audioPath.StartsWith("Packages/", StringComparison.Ordinal) && !includePackages)
                    continue;
                try
                {
                    var importer = AssetImporter.GetAtPath(audioPath) as AudioImporter;
                    if (importer == null)
                        throw new InvalidOperationException("Asset has no compatible importer.");
                    treeElements.Add(new AudioTreeItem("AudioClip", 0, ++idIncrement, audioPath, importer));
                }
                catch (Exception exception) { _analysis.ReportSkipped(audioPath, exception); }
            }

            yield return "Building results table...";
            if (contextKey != ActivePlatform.Current.Key) yield break;
            var treeModel = new TreeModel<AudioTreeItem>(treeElements);
            var treeViewState = new TreeViewState<int>();
            if (_multiColumnHeaderState == null)
                _multiColumnHeaderState = new MultiColumnHeaderState(new[]
                {
                    // when adding a new column don't forget to check the sorting method, and the CellGUI method
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "Audio clip" }, width = 150, minWidth = 150, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "Load type" }, width = 150, minWidth = 150, canSort = true },
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent() { text = "Quality" }, width = 60, minWidth = 60, canSort = true },
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent() { text = "Settings source" }, width = 130, minWidth = 130, canSort = true },
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent() { text = "Format" }, width = 90, minWidth = 90, canSort = true },
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Imported Size", "Imported audio data size for the active platform, matching Unity's audio Inspector."), width = 100, minWidth = 100, canSort = true },
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Ratio", "Imported size / original size × 100%."), width = 75, minWidth = 75, canSort = true },
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Original Size", "Original audio size reported by Unity's audio Inspector; the denominator used for Ratio."), width = 100, minWidth = 100, canSort = true },
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Duration", "Imported clip length in seconds."), width = 85, minWidth = 75, canSort = true },
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Channels", "Channel count of the imported clip, after Force to Mono is applied."), width = 80, minWidth = 70, canSort = true },
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Sample Rate", "Actual sample rate of the imported clip."), width = 100, minWidth = 90, canSort = true },
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Force to Mono", "Whether the importer converts the audio to a single channel."), width = 110, minWidth = 100, canSort = true },
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Preload Audio Data", "Whether the active platform preloads audio data. N/A for Streaming, where this setting is ignored."), width = 140, minWidth = 130, canSort = true },
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Load in Background", "Whether the importer enables asynchronous audio loading."), width = 145, minWidth = 135, canSort = true },
                });
            _audioCompressionTree = new AudioTree(treeViewState, new MultiColumnHeader(_multiColumnHeaderState), treeModel);

            if (OptimizerWindow.EditorWindowInstance != null)
            {
                OptimizerWindow.EditorWindowInstance.Repaint();
            }
        }
    }
}
