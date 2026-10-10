using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Profile;

namespace CrazyGames
{
    public class OptimizerUtils
    {
        /**
         * Find the paths of the scenes that will end up in the final build of the game.
         */
        public static List<string> GetScenesInBuildPath()
        {
            var scenesInBuild = new List<string>();
            var profile = BuildProfile.GetActiveBuildProfile();
            var scenes = profile == null ? EditorBuildSettings.scenes : profile.GetScenesForBuild();
            foreach (var scene in scenes)
            {
                if (scene.enabled)
                {
                    scenesInBuild.Add(scene.path);
                }
            }

            return scenesInBuild;
        }
    }
}
