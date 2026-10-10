using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyGames
{
    [Serializable]
    internal sealed class OptimizationBackup
    {
        public string Batch;
        public string Guid;
        public string Path;
        public string Before;
        public string Expected;
    }

    internal interface IOptimizationImporter
    {
        string Guid { get; }
        string ContextKey { get; }
        string Capture();
        void Apply(OptimizationRule rule);
        void Restore(string settings);
        void Reimport();
    }

    internal interface IOptimizationHistory
    {
        List<OptimizationBackup> Backups { get; }
        void Save();
    }

    internal static class OptimizationBatch
    {
        // One backup/reimport per asset, even when several recommendations are selected.
        public static IEnumerable<string> Apply(IEnumerable<OptimizationRecommendation> issues,
            Func<string, IOptimizationImporter> resolve, IOptimizationHistory history,
            Action<string, Exception> reportError)
        {
            string batch = System.Guid.NewGuid().ToString("N");
            foreach (var group in issues.GroupBy(issue => issue.Path))
            {
                yield return "Applying fixes: " + group.Key;
                IOptimizationImporter importer = null;
                OptimizationBackup backup = null;
                bool savedBackup = false;
                bool mutated = false;
                try
                {
                    importer = resolve(group.Key);
                    string before = importer.Capture();
                    if (group.Any(issue => issue.Guid != importer.Guid || issue.Baseline != before ||
                        (issue.ContextKey != null && issue.ContextKey != importer.ContextKey)))
                        throw new InvalidOperationException("The active build profile, asset, or importer settings changed after review. Refresh recommendations before applying fixes");
                    backup = new OptimizationBackup
                    {
                        Batch = batch, Guid = importer.Guid, Path = group.Key, Before = before, Expected = before
                    };
                    history.Backups.Add(backup);
                    // Make the original settings durable before touching the importer.
                    history.Save();
                    savedBackup = true;
                    mutated = true;
                    foreach (var issue in group)
                        importer.Apply(issue.Rule);
                    string expected = importer.Capture();
                    if (expected == before)
                    {
                        history.Backups.Remove(backup);
                        history.Save();
                        continue;
                    }
                    backup.Expected = expected;
                    // Record the planned state before the reimport can persist it.
                    history.Save();
                    importer.Reimport();
                    backup.Expected = importer.Capture();
                    history.Save();
                }
                catch (Exception exception)
                {
                    if (!savedBackup && backup != null)
                        history.Backups.Remove(backup);
                    if (mutated && importer != null && backup != null)
                    {
                        try
                        {
                            importer.Restore(backup.Before);
                            importer.Reimport();
                            if (savedBackup)
                            {
                                history.Backups.Remove(backup);
                                history.Save();
                            }
                        }
                        catch (Exception rollbackException)
                        {
                            reportError(group.Key, rollbackException);
                        }
                    }
                    reportError(group.Key, exception);
                }
            }
        }

        public static IEnumerable<string> RestoreLast(Func<OptimizationBackup, IOptimizationImporter> resolve,
            IOptimizationHistory history, Action<string, Exception> reportError)
        {
            if (history.Backups.Count == 0)
                yield break;
            string batch = history.Backups[history.Backups.Count - 1].Batch;
            var backups = history.Backups.Where(backup => backup.Batch == batch).ToList();
            foreach (var backup in backups)
            {
                yield return "Restoring settings: " + backup.Path;
                try
                {
                    var importer = resolve(backup);
                    string current = importer.Capture();
                    if (current != backup.Before && current != backup.Expected)
                        throw new InvalidOperationException("Importer settings have changed since this fix. Restore was skipped to preserve newer edits");
                    if (current != backup.Before)
                    {
                        importer.Restore(backup.Before);
                        importer.Reimport();
                    }
                    history.Backups.Remove(backup);
                    history.Save();
                }
                catch (Exception exception) { reportError(backup.Path, exception); }
            }
        }
    }
}
