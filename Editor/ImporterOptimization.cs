using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CrazyGames
{
    internal sealed class ImporterOptimization : IOptimizationImporter
    {
        private readonly AssetImporter _importer;
        private readonly string _platform;
        private readonly string _contextKey;
        public string Guid => AssetDatabase.AssetPathToGUID(_importer.assetPath);
        public string ContextKey => ActivePlatform.Current.Key;

        public ImporterOptimization(string path, string platform = null, string contextKey = null)
        {
            var context = ActivePlatform.Current;
            _platform = platform ?? context.ImporterPlatform;
            _contextKey = contextKey ?? context.Key;
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !AssetDatabase.IsOpenForEdit(path, StatusQueryOptions.ForceUpdate) ||
                !AssetDatabase.IsOpenForEdit(path + ".meta", StatusQueryOptions.ForceUpdate))
                throw new InvalidOperationException("Only editable assets in the Assets folder can be optimized");
            _importer = AssetImporter.GetAtPath(path);
            if (_importer == null)
                throw new InvalidOperationException("Asset importer is missing");
        }

        public string Capture() => EditorJsonUtility.ToJson(_importer);
        public void Restore(string settings) => EditorJsonUtility.FromJsonOverwrite(settings, _importer);
        public void Reimport() => _importer.SaveAndReimport();

        public OptimizationFacts GetFacts(OptimizationAssetKind kind)
        {
            if (ContextKey != _contextKey)
                throw new InvalidOperationException("Active build profile changed. Refresh recommendations.");
            var facts = new OptimizationFacts
            {
                Path = _importer.assetPath, Kind = kind, PlatformName = ActivePlatform.Current.DisplayName,
                ContextKey = _contextKey
            };
            if (kind == OptimizationAssetKind.Texture && _importer is TextureImporter texture)
            {
                var settings = EffectiveTextureSettings(texture, _platform);
                facts.UncompressedTexture = settings.textureCompression == TextureImporterCompression.Uncompressed;
                facts.AutomaticTextureFormat = settings.format == TextureImporterFormat.Automatic;
                facts.TextureMaxSize = settings.maxTextureSize;
            }
            else if (kind == OptimizationAssetKind.Model && _importer is ModelImporter model)
            {
                facts.ModelReadable = model.isReadable;
                facts.OptimizePolygons = model.optimizeMeshPolygons;
                facts.OptimizeVertices = model.optimizeMeshVertices;
                facts.ModelCompressionOff = model.meshCompression == ModelImporterMeshCompression.Off;
            }
            else if (kind == OptimizationAssetKind.Audio && _importer is AudioImporter audio)
            {
                var settings = EffectiveAudioSettings(audio, _platform);
                facts.VorbisAudio = settings.compressionFormat == AudioCompressionFormat.Vorbis;
                facts.AudioQuality = settings.quality;
            }
            else
                throw new InvalidOperationException("Asset type no longer matches the analyzed table");
            return facts;
        }

        internal static TextureImporterPlatformSettings EffectiveTextureSettings(TextureImporter importer, string platform)
        {
            if (string.IsNullOrEmpty(platform)) return importer.GetDefaultPlatformTextureSettings();
            var settings = importer.GetPlatformTextureSettings(platform);
            return settings.overridden ? settings : importer.GetDefaultPlatformTextureSettings();
        }

        internal static AudioImporterSampleSettings EffectiveAudioSettings(AudioImporter importer, string platform)
        {
            return !string.IsNullOrEmpty(platform) && importer.ContainsSampleSettingsOverride(platform) ?
                importer.GetOverrideSampleSettings(platform) : importer.defaultSampleSettings;
        }

        public void Apply(OptimizationRule rule)
        {
            if (ContextKey != _contextKey)
                throw new InvalidOperationException("The active build profile changed. Refresh recommendations before applying fixes");
            if (string.IsNullOrEmpty(_platform) && (rule == OptimizationRule.TextureCompression ||
                rule == OptimizationRule.TextureMaxSize || rule == OptimizationRule.AudioQuality))
                throw new InvalidOperationException("The active platform does not support importer overrides");
            switch (rule)
            {
                case OptimizationRule.TextureCompression:
                case OptimizationRule.TextureMaxSize:
                    var texture = _importer as TextureImporter;
                    if (texture == null) throw new InvalidOperationException("Texture importer is missing");
                    var textureSettings = EffectiveTextureSettings(texture, _platform);
                    textureSettings.name = _platform;
                    textureSettings.overridden = true;
                    if (rule == OptimizationRule.TextureCompression)
                    {
                        if (textureSettings.format != TextureImporterFormat.Automatic)
                            throw new InvalidOperationException("Explicit texture formats must be reviewed manually");
                        textureSettings.textureCompression = TextureImporterCompression.Compressed;
                    }
                    else
                        textureSettings.maxTextureSize = Math.Min(textureSettings.maxTextureSize, 1024);
                    texture.SetPlatformTextureSettings(textureSettings);
                    break;
                case OptimizationRule.AudioQuality:
                    var audio = _importer as AudioImporter;
                    if (audio == null) throw new InvalidOperationException("Audio importer is missing");
                    var audioSettings = EffectiveAudioSettings(audio, _platform);
                    if (audioSettings.compressionFormat != AudioCompressionFormat.Vorbis)
                        throw new InvalidOperationException("Quality fixes require Vorbis compression");
                    audioSettings.quality = Math.Min(audioSettings.quality, 0.7f);
                    if (!audio.SetOverrideSampleSettings(_platform, audioSettings))
                        throw new InvalidOperationException("Unity could not set the active platform audio override");
                    break;
                default:
                    var model = _importer as ModelImporter;
                    if (model == null) throw new InvalidOperationException("Model importer is missing");
                    switch (rule)
                    {
                        case OptimizationRule.ModelReadWrite: model.isReadable = false; break;
                        case OptimizationRule.ModelPolygons: model.optimizeMeshPolygons = true; break;
                        case OptimizationRule.ModelVertices: model.optimizeMeshVertices = true; break;
                        case OptimizationRule.ModelCompression: model.meshCompression = ModelImporterMeshCompression.Low; break;
                        default: throw new ArgumentOutOfRangeException(nameof(rule));
                    }
                    break;
            }
        }
    }

    [Serializable]
    internal sealed class OptimizationHistoryData
    {
        public List<OptimizationBackup> backups = new List<OptimizationBackup>();
    }

    internal sealed class OptimizationHistoryStore : IOptimizationHistory
    {
        private readonly string _path;
        private readonly OptimizationHistoryData _data;
        public List<OptimizationBackup> Backups => _data.backups;

        public OptimizationHistoryStore()
        {
            _path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/WebGLOptimizer/ImporterHistory.json"));
            _data = File.Exists(_path) ? JsonUtility.FromJson<OptimizationHistoryData>(File.ReadAllText(_path)) : new OptimizationHistoryData();
            if (_data == null || _data.backups == null)
                throw new InvalidDataException("Optimization restore history is invalid");
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            string temp = _path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(_data, true));
            if (File.Exists(_path))
                File.Replace(temp, _path, null);
            else
                File.Move(temp, _path);
        }

        public static IOptimizationImporter ResolveBackup(OptimizationBackup backup)
        {
            string path = AssetDatabase.GUIDToAssetPath(backup.Guid);
            if (string.IsNullOrEmpty(path))
                throw new InvalidOperationException("The original asset no longer exists");
            return new ImporterOptimization(path);
        }
    }
}
