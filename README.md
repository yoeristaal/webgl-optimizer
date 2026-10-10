⚠️ This project is no longer maintained. We are moving towards a [custom Unity build script](https://docs.crazygames.com/resources/unity-custom-build/) that will automatically apply the best settings that were suggested by the optimizer, in addition to the other export tips from our [documentation](https://docs.crazygames.com/resources/optimization-tips/). The custom build tool also includes a better assets analyzer, offering more tips and one-click quick fix buttons.

# Unity WebGL Optimizer

The purpose of this package is to help you optimize your game by reducing the build size and increasing the performance. At the moment, it is targeted at WebGL games.

If you are using our CrazySDK, this package will come integrated with it.

Minimum required versions:

- C# 6.0
- Unity 2019

Once you have imported the package from GitHub, or our SDK which also contains this tool, it will be accessible in the `Tools > WebGL Optimizer` menu option.

![Menu option](Docs/menu-option.png?raw=true)

## Export optimizations

The export optimizations tab contains a checklist of options that should be correctly set to improve the performance and decrease the bundle size of your WebGL game.

![Export optimizations](Docs/export.png?raw=true "Export optimizations")

## Texture optimizations

The texture optimizations tool provides an overview of all the textures in your project, and also various tips about optimizing the size they occupy in the final build.

It finds textures in your project in these 2 ways:

1. By looking at the scenes included in the build (Build settings > Scenes in build), and finding recursively all the textures on which those scenes depend.
2. By finding textures in `Resources` folders, or by recursively finding textures on which the assets from the Resources folders depend.

This means that the texture detection may miss more intricate textures that are not covered by the above cases.

You can toggle the "Include files from Packages" options to also display textures from the installed packages, for example from Package Manager.

![Texture optimizations](Docs/textures.png?raw=true "Texture optimizations")

## Recommendations and batch fixes

After analyzing textures, models, or audio clips, expand **Recommendations and batch fixes** and click **Find / refresh recommendations**. Recommendations are ranked by severity and explain their proposed setting changes and tradeoffs.

The texture and audio tables show effective settings for the active build profile's platform. **Settings source** shows **Default** when no override is enabled, or the platform override (for example, **Windows override** or **WebGL override**). Format, compression, size, load type, and quality values come from that source. Windows, macOS, and Linux share Unity's Standalone override. Model settings apply to every platform. Analysis uses the active profile's scene list and refreshes after switching profiles.

Recommendations appear as a summary grouped by fix type, such as compression, maximum texture size of 1024 px, or Crunch compression for compatible automatic 2D texture formats. Each group shows the number of affected assets. Click **Preview batch fix for all … assets**, review the group, then click **Execute fixes on … assets** above the summary. No individual checks are required. Counts can overlap because one asset may need several fixes.

For a smaller batch, select assets in the table and click **Preview fixes for selected assets**. **View affected files** opens an optional detail view with individual checks and **Preview this fix** buttons. Changes are grouped by asset so each asset is reimported once. Texture and audio fixes create or update the active platform override; model fixes affect every platform. Switching profiles clears previews and prevents applying stale recommendations. Package assets and assets that are not editable are excluded from fixes. Asset analysis and recommendations refresh automatically after a batch or restore finishes.

**Restore last batch** restores saved importer settings. It skips assets whose settings have since changed, preserving newer edits. Restore history is kept in `Library/WebGLOptimizer/ImporterHistory.json` and survives editor reloads; deleting the Library folder deletes that history. Cancelling a batch stops remaining work, and completed changes remain restorable. Individual Unity reimports finish before cancellation takes effect.

## Model optimizations

The model optimizations tool works very similar to the texture optimizations tool, but instead of textures it finds models in your project and provides various tips about optimizing them.

![Model optimizations](Docs/models.png?raw=true "Model optimizations")

## Audio clip optimizations

The audio clip optimizations tool works very similar to the texture optimizations tool, but instead of textures it finds audio clips in your project and provides various tips about optimizing them.

![Audio optimizations](Docs/audio.png?raw=true "Audio optimizations")

## Build logs analyzer

The build logs analyzer parses the Editor.log file to extract the list of files included in your build and the space they occupy. You can use this utility to furthermore analyze the files included in your project.

Similar to the texture optimizer, you can toggle the "Include files from Packages" options to also display textures from the installed packages, for example from Package Manager.

![Texture optimizations](Docs/build-logs.png?raw=true "Build logs")

## CrazyGames

We can also help your game become popular on the web. You can submit your game on CrazyGames [here](https://developer.crazygames.com/).
