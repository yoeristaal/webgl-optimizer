# Changelog

## [2.1.0] - 2026-10-10

### Added
- Ranked texture, model, and audio recommendations with severity, explanations, and exact change previews.
- Batch fixes for checked recommendations, including selection of recommendations for assets selected in a table.
- Persistent importer backups and a Restore last batch action that preserves newer manual edits.
- Cancellable recommendation scans and batch operations, with automatic table refresh after changes.

## [2.0.8] - 2026-10-10

### Added
- Cancellable analysis in editor-update batches for textures, models, audio clips, build profiles, and build logs.
- Live progress messages and summaries of skipped assets, with paths and reasons in the Console.

### Fixed
- Analysis state and update subscriptions are cleaned up on completion, failure, cancellation, window close, editor shutdown, and assembly reload.
- Failed or cancelled analysis retains the previous results.
- Empty or stale tree selections no longer throw exceptions.
- Build-log analysis handles missing sections, truncated reports, and malformed asset entries without indexing empty lists.
- Build-log reading uses Unity's current log path and releases the reader on cancellation.

## [2.0.7] - 2026-10-10

### Fixed
- Replaced obsolete `TreeViewState` construction with `TreeViewState<int>` in all analysis panels.
