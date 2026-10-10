# Changelog

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
