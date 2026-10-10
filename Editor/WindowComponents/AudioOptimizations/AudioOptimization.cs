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
        private static bool _includeFilesFromPackages;

        public static void RenderGUI()
        {
            var rect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(300));
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Press \"Analyze audio\" button to load the table.");
            GUILayout.Label("Press it again when you need to refresh the data.");
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
            EditorGUILayout.Space(5);

            GUILayout.Label(
                "This utility gives you an overview of the audio clips used in your project. By optimizing various settings, you will be able to considerably decrease your final build size and runtime memory usage. You can click on an audio clip to select it in the Project view. To find out more about how the tool finds the audio clips, please check our GitHub repo.",
                EditorStyles.wordWrappedLabel);


            BuildExplanation("Load type",
                "The default option, Decompress On Load, is good for audio clips that require precision when played, for example, audio effects or dialogues. For background audio clips Compressed In Memory is recommended, since it reduces the runtime memory, though audio playback is less precise and may introduce latency.");
            BuildExplanation("Quality",
                "Lowering the quality will reduce the build size. You can experiment with a lower audio quality for background audio.");
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

        static void AnalyzeAudio()
        {
            bool includePackages = _includeFilesFromPackages;
            _analysis.Start(() => AnalyzeAudioSteps(includePackages));
        }

        static IEnumerator<string> AnalyzeAudioSteps(bool includePackages)
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
                });
            _audioCompressionTree = new AudioTree(treeViewState, new MultiColumnHeader(_multiColumnHeaderState), treeModel);

            if (OptimizerWindow.EditorWindowInstance != null)
            {
                OptimizerWindow.EditorWindowInstance.Repaint();
            }
        }
    }
}