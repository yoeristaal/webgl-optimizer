using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrazyGames.WindowComponents
{
    internal sealed class ExportRecommendation
    {
        public string Title, Current, Recommended, Explanation;
        public bool Satisfied;
        public Action Fix;
    }

    public class ExportOptimizations
    {
        private static string _message, _messageContext;
        private static MessageType _messageType;

        internal static List<ExportRecommendation> GetRecommendations()
        {
            var items = new List<ExportRecommendation>();
            if (ActivePlatform.Current.Target != BuildTarget.WebGL) return items;
            Add(items, "Brotli compression", PlayerSettings.WebGL.compressionFormat, WebGLCompressionFormat.Brotli,
                () => PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli,
                "Smaller downloads. Your host must serve the correct Content-Encoding header and support Brotli over HTTPS.");
            Add(items, "Name files as hashes", PlayerSettings.WebGL.nameFilesAsHashes, true,
                () => PlayerSettings.WebGL.nameFilesAsHashes = true,
                "Changes filenames when content changes. Update any deployment scripts that rely on fixed build filenames.");
            var exceptions = PlayerSettings.WebGL.exceptionSupport;
            Add(items, "Exception support", exceptions, WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly,
                () => PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly,
                "Reduces exception overhead compared with full support. Test exception handling and diagnostics before release.",
                exceptions == WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly || exceptions == WebGLExceptionSupport.None);
            Add(items, "Strip engine code", PlayerSettings.stripEngineCode, true,
                () => PlayerSettings.stripEngineCode = true,
                "Removes unused engine code. Test the built game, especially features reached through reflection or plugins.");
            Add(items, "Data caching", PlayerSettings.WebGL.dataCaching, true,
                () => PlayerSettings.WebGL.dataCaching = true,
                "Caches content for repeat visits. Browser storage restrictions can limit caching; check your hosting and update flow.");
            Add(items, "Release debug symbols", PlayerSettings.WebGL.debugSymbolMode, WebGLDebugSymbolMode.Off,
                () => PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off,
                "Disables exported debug symbols for release builds. Keep symbols enabled when diagnosing problems.");
            var stripping = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL);
            Add(items, "Managed code stripping", stripping, ManagedStrippingLevel.Medium,
                () => PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium),
                "Uses Medium stripping to reduce code size. Preserve reflection-dependent types with link.xml or Preserve attributes and test the build. High is also accepted.",
                stripping == ManagedStrippingLevel.Medium || stripping == ManagedStrippingLevel.High);
            Add(items, "IL2CPP code generation", PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.WebGL), Il2CppCodeGeneration.OptimizeSize,
                () => PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize),
                "Optimizes generated code for size and build time. Measure runtime performance before keeping this setting.");
            Add(items, "Optimize mesh data", PlayerSettings.stripUnusedMeshComponents, true,
                () => PlayerSettings.stripUnusedMeshComponents = true,
                "Strips unused vertex attributes. Check meshes used by shaders or materials swapped at runtime.");
            return items;
        }

        private static void Add<T>(List<ExportRecommendation> items, string title, T current, T recommended,
            Action fix, string explanation, bool? satisfied = null)
        {
            items.Add(new ExportRecommendation
            {
                Title = title, Current = current.ToString(), Recommended = recommended.ToString(),
                Satisfied = satisfied ?? EqualityComparer<T>.Default.Equals(current, recommended), Fix = fix, Explanation = explanation
            });
        }

        // Unity's PlayerSettings API reads/writes the active profile override when one exists.
        // Its YAML must be serialized back into the profile asset before saving.
        internal static string SettingsSource(BuildProfile profile)
        {
            return HasPlayerSettingsOverride(profile) ? "Active build profile override" : "Shared Player Settings";
        }

        private static bool HasPlayerSettingsOverride(BuildProfile profile)
        {
            if (profile == null) return false;
            var method = typeof(BuildProfile).GetMethod("HasSerializedPlayerSettings", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("Cannot determine the active profile's Player Settings source in this Unity version.");
            return (bool)method.Invoke(profile, null);
        }

        internal static void ApplyFix(ExportRecommendation item, string contextKey)
        {
            if (ActivePlatform.Current.Target != BuildTarget.WebGL || ActivePlatform.Current.Key != contextKey)
                throw new InvalidOperationException("The active build profile changed. Review the export recommendations again.");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Wait until Unity finishes compiling, importing, or building.");
            var profile = BuildProfile.GetActiveBuildProfile();
            MethodInfo serialize = null;
            if (HasPlayerSettingsOverride(profile))
            {
                serialize = typeof(BuildProfile).GetMethod("SerializePlayerSettings", BindingFlags.Instance | BindingFlags.NonPublic);
                if (serialize == null) throw new InvalidOperationException("Cannot save Player Settings overrides in this Unity version.");
                Undo.RecordObject(profile, "Optimize export: " + item.Title);
            }
            if (item.Satisfied) return;
            item.Fix();
            if (serialize != null)
            {
                serialize.Invoke(profile, null);
                EditorUtility.SetDirty(profile);
            }
            AssetDatabase.SaveAssets();
            if (!GetRecommendations().Single(recommendation => recommendation.Title == item.Title).Satisfied)
                throw new InvalidOperationException("Unity did not retain the recommended value. Open Player Settings to review it.");
        }

        public static void RenderGUI()
        {
            var context = ActivePlatform.Current;
            ActivePlatform.RenderContext(false);
            if (context.Target != BuildTarget.WebGL)
            {
                EditorGUILayout.HelpBox("These export recommendations apply to WebGL. Activate a WebGL build profile to inspect and fix its settings.", MessageType.Info);
                return;
            }
            string source;
            try { source = SettingsSource(BuildProfile.GetActiveBuildProfile()); }
            catch (Exception exception) { EditorGUILayout.HelpBox(exception.Message, MessageType.Error); return; }
            EditorGUILayout.LabelField("Settings source: " + source, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Review each change for your release build. Shared Player Settings also affect profiles that inherit them.", EditorStyles.wordWrappedLabel);
            var items = GetRecommendations();
            EditorGUILayout.LabelField($"{items.Count(item => item.Satisfied)} / {items.Count} recommendations satisfied", EditorStyles.boldLabel);
            if (_messageContext == context.Key && !string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, _messageType);
            bool busy = EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer;
            foreach (var item in items)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField((item.Satisfied ? "OK: " : "Review: ") + item.Title, EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(busy || item.Satisfied))
                    if (GUILayout.Button("Fix: " + item.Title, GUILayout.Width(220)))
                    {
                        _messageContext = context.Key;
                        try
                        {
                            ApplyFix(item, context.Key);
                            _message = item.Title + " saved. Export recommendations have been refreshed.";
                            _messageType = MessageType.Info;
                        }
                        catch (Exception exception)
                        {
                            _message = "Could not apply " + item.Title + ": " + exception.Message;
                            _messageType = MessageType.Error;
                            Debug.LogException(exception);
                        }
                        OptimizerWindow.EditorWindowInstance?.Repaint();
                        // End this layout normally; the next repaint reads all settings again.
                    }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField($"Current: {item.Current}    Recommended: {item.Recommended}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(item.Explanation, EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndVertical();
            }
            var graphicsSettings = GraphicsSettings.GetGraphicsSettings();
            if (graphicsSettings != null)
                using (var serialized = new SerializedObject(graphicsSettings))
                {
                    var preloaded = serialized.FindProperty("m_PreloadedShaders");
                    if (preloaded != null && preloaded.arraySize > 0)
                        EditorGUILayout.HelpBox($"Shared Graphics Settings preloads {preloaded.arraySize} shader collection(s). Review startup cost before keeping or removing these collections.", MessageType.Info);
                }
            EditorGUILayout.HelpBox("Review preloaded shaders and unused render pipeline features in Graphics Settings. Their value depends on the game, so inspect them before removing anything.", MessageType.Info);
            if (GUILayout.Button("Open Graphics Settings")) SettingsService.OpenProjectSettings("Project/Graphics");
            if (GUILayout.Button("Read Unity Web optimization guidance"))
                Application.OpenURL("https://docs.unity3d.com/6000.3/Documentation/Manual/web-optimization-player.html");
        }
    }
}
