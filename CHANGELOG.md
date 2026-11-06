# Changelog

## [2.9.2] - 2026-11-06

### Fixed
- Define an Editor-only assembly for the Unity package so its tree and build-log helpers compile alongside the optimizer.

## [2.9.1] - 2026-11-03

### Fixed
- Batch build-report reading and parsing with bounded work slices, avoiding the 100-lines-per-Editor-update bottleneck on large logs while retaining cancellation and progress updates.

## [2.9.0] - 2026-11-03

### Added
- Sortable Build size columns in Textures, Models, Materials, and Audio clips using asset bytes from the latest complete Editor.log build report, with N/A for unavailable or embedded-material sizes and explanations of report freshness and compression.

### Fixed
- Recalculate build-log percentages from all reported asset sizes, including hidden package assets, so invalid Unity percentages cannot produce shares above 100%; clarify the column as Asset share %.

## [2.8.1] - 2026-11-01

### Fixed
- Build-log analysis accepts finite nonnegative percentages above 100 emitted in Unity asset reports, preventing valid rows from being skipped. Invalid sizes and negative/nonfinite percentages remain rejected.

## [2.8.0] - 2026-10-24

### Added
- Materials tab with automatic, cancellable analysis of build-scene and Resources dependencies, including embedded model materials and optional package assets.
- Searchable, sortable material columns for shader, render queue, shader passes, GPU instancing, keywords, texture count, Editor texture memory, double-sided GI, and asset path; select materials directly in the Inspector.

## [2.7.0] - 2026-10-24

### Added
- Subtle orange texture row highlights for settings flagged by the existing optimization recommendations, with hover explanations and updates when effective settings change.

## [2.6.1] - 2026-10-24

### Fixed
- Fixed texture size column compilation on Unity versions where the pending texture compression flag is internal, using optional reflection access.

## [2.6.0] - 2026-10-23

### Added
- Sortable Compressed size column in the Textures tab showing imported texture storage size for the active platform, with readable byte units and N/A for unavailable data.
- Texture size tooltips and explanations; sizes refresh after reimports and active platform changes.

## [2.5.0] - 2026-10-19

### Added
- Sortable audio columns for Imported Size, Ratio, Original Size, Duration, Channels, Sample Rate, Force to Mono, Preload Audio Data, and Load in Background.
- Audio clip asset paths on hover, column tooltips, and explanations of audio size and loading settings.
- Project instructions requiring agents to update the changelog and increase the package version whenever they change the project.

### Changed
- Audio size and clip metadata refresh after reimports and active platform changes; unavailable data displays N/A.

## [2.4.2] - 2026-10-11

### Changed
- Texture, model, audio, build profile, and build log tabs automatically start analysis on their first opening.
- Manual refresh and cancellation remain available. Repaints and revisiting a tab do not restart analysis; failed or cancelled initial runs require a manual retry.

## [2.4.1] - 2026-10-11

### Fixed
- Batch fixes and restores group imports in chunks of 32 assets instead of triggering a separate import cycle for each asset.
- Restore history is saved a few times per chunk rather than rewritten for every asset; backups remain durable before imports start.
- Import scopes always close before yielding, including on failure, so cancellation between chunks cannot leave Unity importing paused.
- Post-import checks reacquire importer objects and retain backups when an import flush fails.

## [2.4.0] - 2026-10-11

### Added
- Export fixes for data caching, release debug symbols, Medium managed code stripping, IL2CPP code size, and unused mesh data.
- Current/recommended values, tradeoff explanations, and a satisfaction summary for the export checklist.

### Fixed
- Export fixes require an active WebGL profile, show whether Player Settings are shared or overridden, save changes, and immediately recheck recommendations.
- Existing High stripping and disabled exception support are accepted without downgrading them.

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
