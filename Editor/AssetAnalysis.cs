using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace CrazyGames
{
    internal static class AssetAnalysis
    {
        public static IEnumerable<string> CollectPaths(Func<string, bool> matches,
            HashSet<string> result, AnalysisJob job)
        {
            foreach (string scene in OptimizerUtils.GetScenesInBuildPath())
            {
                yield return "Scanning scene: " + scene;
                foreach (string progress in CollectDependencies(scene, matches, result, job))
                    yield return progress;
            }

            yield return "Finding Resources assets...";
            var guids = AssetDatabase.FindAssets("", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                yield return $"Checking Resources assets: {i + 1}/{guids.Length}";
                var segments = path.Split('/');
                if (!segments.Contains("Resources") || segments.Contains("Editor") || AssetDatabase.IsValidFolder(path))
                    continue;
                foreach (string progress in CollectDependencies(path, matches, result, job))
                    yield return progress;
            }
        }

        private static IEnumerable<string> CollectDependencies(string path, Func<string, bool> matches,
            HashSet<string> result, AnalysisJob job)
        {
            string[] dependencies;
            try { dependencies = AssetDatabase.GetDependencies(path, true); }
            catch (Exception exception)
            {
                job.ReportSkipped(path, exception);
                dependencies = new string[0];
            }
            foreach (string dependency in dependencies)
            {
                yield return "Inspecting: " + dependency;
                if (result.Contains(dependency))
                    continue;
                try
                {
                    if (matches(dependency))
                        result.Add(dependency);
                }
                catch (Exception exception) { job.ReportSkipped(dependency, exception); }
            }
        }
    }
}
