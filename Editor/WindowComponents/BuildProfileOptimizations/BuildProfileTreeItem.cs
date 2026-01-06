using CrazyGames.TreeLib;
using UnityEditor;
using UnityEditor.Build.Profile;
using System.Linq;

namespace CrazyGames.WindowComponents.BuildProfileOptimizations
{
    public class BuildProfileTreeItem : TreeElement
    {
        public BuildProfile Profile { get; }
        public string ProfileName { get; }
        public string PlatformName { get; }
        public string GameName { get; }
        public bool IsActive { get; }
        public int SceneCount { get; }
        public string FirstScene { get; }

        public BuildProfileTreeItem(string name, int depth, int id, BuildProfile profile) : base(name, depth, id)
        {
            if (depth == -1)
                return;

            Profile = profile;
            ProfileName = profile.name;

            // Parse platform and game name from the profile name
            // Format: "Platform - GameName" (e.g., "Web - Independence", "Poki - Colorizer")
            var parts = ProfileName.Split(new[] { " - " }, System.StringSplitOptions.None);
            if (parts.Length >= 2)
            {
                PlatformName = parts[0].Trim();
                GameName = parts[1].Trim();
            }
            else
            {
                // If no dash separator, use the whole name as platform
                PlatformName = ProfileName;
                GameName = "";
            }

            // Check if this is the active profile
            try
            {
                IsActive = BuildProfile.GetActiveBuildProfile() == profile;
            }
            catch
            {
                IsActive = false;
            }

            // Get the scenes for this build profile
            var scenesForBuild = profile.GetScenesForBuild();
            SceneCount = scenesForBuild != null ? scenesForBuild.Length : 0;

            // Get the first scene
            if (scenesForBuild != null && scenesForBuild.Length > 0)
            {
                var firstScene = scenesForBuild[0];
                // Extract just the filename without path and extension from the scene path
                FirstScene = System.IO.Path.GetFileNameWithoutExtension(firstScene.path);
            }
            else
            {
                FirstScene = "(none)";
            }
        }
    }
}