# Changelog

## [2.3.0] - 2026-10-11

### Added
- Recommendation summaries grouped by fix type, with affected asset counts and batch preview/execution buttons for each group.
- Direct previews for assets selected in the table and individual fixes, with the execution button above the summary.
- Crunch compression recommendations and batch fixes for compatible automatic 2D texture formats on the active platform.

### Changed
- Individual recommendations are now an optional detail view instead of the default long list.
- Asset analysis and recommendation scans run again automatically after batch execution or restoration, including partial batches.

## [2.2.1] - 2026-10-11

### Fixed
- Corrected the invalid GUID in RecommendationPanel.cs.meta that caused Unity to ignore the script and report missing RecommendationPanel errors in the texture, model, and audio tabs.

## [2.2.0] - 2026-10-10

### Added
- Active build profile and platform indicators in texture, audio, and model tabs.
- Settings source and format columns showing Default or the active platform override.

### Fixed
- Texture and audio tables and recommendations use the active platform's effective importer settings, falling back to defaults when no override is enabled.
- Analysis uses the active build profile's scene list and refreshes when profiles change.
- Batch fixes target the reviewed platform and reject previews after switching build profiles. Desktop overrides use Unity's shared Standalone settings.

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
