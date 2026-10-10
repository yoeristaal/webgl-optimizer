using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;

namespace CrazyGames
{
    internal sealed class PlatformContext
    {
        public BuildTarget Target;
        public string ImporterPlatform;
        public string DisplayName;
        public string ProfileName;
        public string Key;
    }

    [InitializeOnLoad]
    internal static class ActivePlatform
    {
        private static string _lastKey;
        private static double _nextRefresh;
        public static int Revision { get; private set; }

        static ActivePlatform()
        {
            EditorApplication.update += Poll;
            EditorApplication.projectChanged += Invalidate;
        }

        public static PlatformContext Current
        {
            get
            {
                var target = EditorUserBuildSettings.activeBuildTarget;
                var profile = BuildProfile.GetActiveBuildProfile();
                string platform = ImporterPlatformFor(target);
                string profilePath = profile == null ? "" : AssetDatabase.GetAssetPath(profile);
                string profileName = profile == null ? "Platform profile" : profile.name;
                return new PlatformContext
                {
                    Target = target, ImporterPlatform = platform, DisplayName = DisplayNameFor(target),
                    ProfileName = profileName, Key = target + "|" + profilePath + "|" + profileName
                };
            }
        }

        internal static string ImporterPlatformFor(BuildTarget target)
        {
            var group = BuildPipeline.GetBuildTargetGroup(target);
            if (group == BuildTargetGroup.Unknown) return null;
            // Importer overrides use the group name: all desktop targets use Standalone.
            return NamedBuildTarget.FromBuildTargetGroup(group).TargetName;
        }

        internal static string DisplayNameFor(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64: return "Windows";
                case BuildTarget.StandaloneOSX: return "macOS";
                case BuildTarget.StandaloneLinux64: return "Linux";
                case BuildTarget.WebGL: return "WebGL";
                default: return target.ToString();
            }
        }

        private static void Invalidate() { Revision++; }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextRefresh) return;
            _nextRefresh = EditorApplication.timeSinceStartup + 0.25;
            string key = Current.Key;
            if (_lastKey != key)
            {
                _lastKey = key;
                Revision++;
            }
            if (OptimizerWindow.EditorWindowInstance != null)
                OptimizerWindow.EditorWindowInstance.Repaint();
        }

        public static void RenderContext(bool showSettingsSource = true)
        {
            var context = Current;
            EditorGUILayout.LabelField($"Active profile: {context.ProfileName} • Platform: {context.DisplayName}", EditorStyles.boldLabel);
            if (showSettingsSource)
                EditorGUILayout.LabelField("Settings source identifies Default settings or the active platform override.", EditorStyles.wordWrappedLabel);
        }
    }
}
