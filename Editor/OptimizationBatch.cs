using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace CrazyGames
{
    [Serializable]
    internal sealed class OptimizationBackup
    {
        public string Batch, Guid, Path, Before, Expected;
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
        internal const int DefaultChunkSize = 32;

        private sealed class ImportScope : IDisposable
        {
            public ImportScope() { AssetDatabase.StartAssetEditing(); }
            public void Dispose() { AssetDatabase.StopAssetEditing(); }
        }

        private sealed class Pending
        {
            public IOptimizationImporter Importer;
            public OptimizationBackup Backup;
            public OptimizationRecommendation[] Issues;
            public string RestoreFrom;
            public bool Changed, Rollback;
        }

        // No yields inside an import scope: Unity always resumes importing before the
        // scheduler can cancel, switch profiles, close the window, or reload scripts.
        public static IEnumerable<string> Apply(IEnumerable<OptimizationRecommendation> issues,
            Func<string, IOptimizationImporter> resolve, IOptimizationHistory history,
            Action<string, Exception> reportError, int chunkSize = DefaultChunkSize)
        {
            if (chunkSize < 1) throw new ArgumentOutOfRangeException(nameof(chunkSize));
            var groups = issues.GroupBy(issue => issue.Path).ToArray();
            string batch = System.Guid.NewGuid().ToString("N");
            for (int offset = 0; offset < groups.Length; offset += chunkSize)
            {
                int count = Math.Min(chunkSize, groups.Length - offset);
                yield return $"Applying and importing assets {offset + 1}–{offset + count} of {groups.Length}...";
                ApplyChunk(groups.Skip(offset).Take(count), batch, resolve, history, reportError);
            }
        }

        private static void ApplyChunk(IEnumerable<IGrouping<string, OptimizationRecommendation>> groups, string batch,
            Func<string, IOptimizationImporter> resolve, IOptimizationHistory history, Action<string, Exception> error)
        {
            var pending = new List<Pending>();
            foreach (var group in groups)
            {
                try
                {
                    var importer = resolve(group.Key);
                    string before = importer.Capture();
                    if (group.Any(issue => issue.Guid != importer.Guid || issue.Baseline != before ||
                        (issue.ContextKey != null && issue.ContextKey != importer.ContextKey)))
                        throw new InvalidOperationException("The active build profile, asset, or importer settings changed after review. Refresh recommendations before applying fixes");
                    pending.Add(new Pending
                    {
                        Importer = importer, Issues = group.ToArray(),
                        Backup = new OptimizationBackup { Batch = batch, Guid = importer.Guid, Path = group.Key, Before = before, Expected = before }
                    });
                }
                catch (Exception exception) { error(group.Key, exception); }
            }
            if (pending.Count == 0) return;
            foreach (var item in pending) history.Backups.Add(item.Backup);
            // Save all originals before any settings change; never rewrite the full file per asset.
            try { history.Save(); }
            catch (Exception exception)
            {
                foreach (var item in pending) { history.Backups.Remove(item.Backup); error(item.Backup.Path, exception); }
                return;
            }
            foreach (var item in pending)
            {
                try
                {
                    foreach (var issue in item.Issues) item.Importer.Apply(issue.Rule);
                    item.Backup.Expected = item.Importer.Capture();
                    item.Changed = item.Backup.Expected != item.Backup.Before;
                    if (!item.Changed) history.Backups.Remove(item.Backup);
                }
                catch (Exception exception)
                {
                    error(item.Backup.Path, exception);
                    try
                    {
                        item.Importer.Restore(item.Backup.Before);
                        if (item.Importer.Capture() != item.Backup.Before) throw new InvalidOperationException("Rollback did not restore importer settings");
                        history.Backups.Remove(item.Backup);
                    }
                    catch (Exception rollbackException)
                    {
                        error(item.Backup.Path, rollbackException);
                        try { item.Backup.Expected = item.Importer.Capture(); }
                        catch (Exception captureException) { error(item.Backup.Path, captureException); }
                    }
                }
            }
            // Planned states are durable before queuing imports, including after an editor crash.
            try { history.Save(); }
            catch (Exception exception)
            {
                foreach (var item in pending)
                {
                    try { item.Importer.Restore(item.Backup.Before); }
                    catch (Exception rollbackException) { error(item.Backup.Path, rollbackException); }
                    error(item.Backup.Path, exception);
                }
                // The first save still contains the original settings. Leave those backups
                // available; no import was queued and a later restore can reconcile them.
                throw new InvalidOperationException("Could not save planned batch settings. Imports were not started.", exception);
            }
            var changed = pending.Where(item => item.Changed).ToArray();
            if (changed.Length == 0) return;
            try
            {
                using (new ImportScope())
                    foreach (var item in changed)
                        try { item.Importer.Reimport(); }
                        catch (Exception exception)
                        {
                            error(item.Backup.Path, exception);
                            try
                            {
                                item.Importer.Restore(item.Backup.Before);
                                item.Importer.Reimport();
                                item.Rollback = true;
                            }
                            catch (Exception rollbackException) { error(item.Backup.Path, rollbackException); }
                        }
            }
            catch (Exception exception)
            {
                // A failed import flush must retain every durable backup for recovery.
                foreach (var item in changed) error(item.Backup.Path, exception);
                throw;
            }
            foreach (var item in changed)
            {
                try
                {
                    // Imports can replace importer objects or trigger postprocessors.
                    var current = resolve(item.Backup.Path);
                    if (current.Guid != item.Backup.Guid) throw new InvalidOperationException("Asset identity changed during import");
                    string actual = current.Capture();
                    if (item.Rollback && actual == item.Backup.Before) history.Backups.Remove(item.Backup);
                    else item.Backup.Expected = actual;
                }
                catch (Exception exception) { error(item.Backup.Path, exception); }
            }
            history.Save();
        }

        public static IEnumerable<string> RestoreLast(Func<OptimizationBackup, IOptimizationImporter> resolve,
            IOptimizationHistory history, Action<string, Exception> reportError, int chunkSize = DefaultChunkSize)
        {
            if (chunkSize < 1) throw new ArgumentOutOfRangeException(nameof(chunkSize));
            if (history.Backups.Count == 0) yield break;
            string batch = history.Backups[history.Backups.Count - 1].Batch;
            var backups = history.Backups.Where(backup => backup.Batch == batch).ToArray();
            for (int offset = 0; offset < backups.Length; offset += chunkSize)
            {
                int count = Math.Min(chunkSize, backups.Length - offset);
                yield return $"Restoring and importing assets {offset + 1}–{offset + count} of {backups.Length}...";
                RestoreChunk(backups.Skip(offset).Take(count), resolve, history, reportError);
            }
        }

        private static void RestoreChunk(IEnumerable<OptimizationBackup> backups,
            Func<OptimizationBackup, IOptimizationImporter> resolve, IOptimizationHistory history, Action<string, Exception> error)
        {
            var pending = new List<Pending>();
            foreach (var backup in backups)
            {
                try
                {
                    var importer = resolve(backup);
                    if (importer.Guid != backup.Guid) throw new InvalidOperationException("Asset identity changed since the fix");
                    string current = importer.Capture();
                    if (current != backup.Before && current != backup.Expected)
                        throw new InvalidOperationException("Importer settings have changed since this fix. Restore was skipped to preserve newer edits");
                    if (current == backup.Before) { history.Backups.Remove(backup); continue; }
                    pending.Add(new Pending { Importer = importer, Backup = backup, RestoreFrom = current });
                }
                catch (Exception exception) { error(backup.Path, exception); }
            }
            if (pending.Count > 0)
                using (new ImportScope())
                    foreach (var item in pending)
                        try { item.Importer.Restore(item.Backup.Before); item.Importer.Reimport(); item.Changed = true; }
                        catch (Exception exception)
                        {
                            error(item.Backup.Path, exception);
                            try
                            {
                                item.Importer.Restore(item.RestoreFrom);
                                item.Importer.Reimport();
                                item.Rollback = true;
                                item.Changed = true;
                            }
                            catch (Exception rollbackException) { error(item.Backup.Path, rollbackException); }
                        }
            foreach (var item in pending.Where(item => item.Changed))
            {
                try
                {
                    var current = resolve(item.Backup);
                    if (current.Guid != item.Backup.Guid) throw new InvalidOperationException("Asset identity changed during restore import");
                    string actual = current.Capture();
                    if (item.Rollback) item.Backup.Expected = actual;
                    else if (actual == item.Backup.Before) history.Backups.Remove(item.Backup);
                    else
                    {
                        item.Backup.Expected = actual;
                        error(item.Backup.Path, new InvalidOperationException("An import postprocessor changed the restored settings. The backup was retained."));
                    }
                }
                catch (Exception exception) { error(item.Backup.Path, exception); }
            }
            history.Save();
        }
    }
}
