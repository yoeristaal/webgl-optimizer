using CrazyGames.TreeLib;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CrazyGames.WindowComponents.AudioOptimizations
{
    public class AudioTreeItem : TreeElement
    {
        public string AudioPath { get; }
        public double? BuildSize { get; internal set; }
        public string AudioName { get; }

        public string LoadType
        {
            get
            {
                Refresh();
                switch (_platformSettings.loadType)
                {
                    case AudioClipLoadType.DecompressOnLoad:
                        return "Decompress on load";
                    case AudioClipLoadType.CompressedInMemory:
                        return "Compressed in memory";
                    case AudioClipLoadType.Streaming:
                        return "Streaming";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public int Quality { get { Refresh(); return Mathf.RoundToInt(_platformSettings.quality * 100); } }
        public string SettingsSource { get { Refresh(); return _source; } }
        public string CompressionFormat { get { Refresh(); return _platformSettings.compressionFormat.ToString(); } }
        public long? ImportedSize { get { Refresh(); return _importedSize; } }
        public double? Ratio { get { Refresh(); return _ratio; } }
        public long? OriginalSize { get { Refresh(); return _originalSize; } }
        public float? Duration { get { Refresh(); return _duration; } }
        public int? Channels { get { Refresh(); return _channels; } }
        public int? SampleRate { get { Refresh(); return _sampleRate; } }
        public bool? ForceToMono { get { Refresh(); return _audioImporter != null ? (bool?)_audioImporter.forceToMono : null; } }
        public bool? PreloadAudioData { get { Refresh(); return _audioImporter != null && _platformSettings.loadType != AudioClipLoadType.Streaming ? (bool?)_platformSettings.preloadAudioData : null; } }
        public bool? LoadInBackground { get { Refresh(); return _audioImporter != null ? (bool?)_audioImporter.loadInBackground : null; } }

        private AudioImporter _audioImporter;
        private AudioImporterSampleSettings _platformSettings;
        private string _contextKey, _source;
        private int _revision = -1;
        private double _nextRefresh;
        private long? _importedSize;
        private double? _ratio;
        private long? _originalSize;
        private float? _duration;
        private int? _channels, _sampleRate;

        private void Refresh()
        {
            if (AudioPath == null) return;
            var context = ActivePlatform.Current;
            if (_contextKey == context.Key && _revision == ActivePlatform.Revision && EditorApplication.timeSinceStartup < _nextRefresh) return;
            bool refreshMetadata = _contextKey != context.Key || _revision != ActivePlatform.Revision;
            _contextKey = context.Key;
            _revision = ActivePlatform.Revision;
            _nextRefresh = EditorApplication.timeSinceStartup + 0.25;
            _audioImporter = AssetImporter.GetAtPath(AudioPath) as AudioImporter;
            if (_audioImporter == null)
            {
                _source = "Unavailable";
                _platformSettings = default;
                _importedSize = null;
                _ratio = null;
                _originalSize = null;
                _duration = null;
                _channels = null;
                _sampleRate = null;
                return;
            }
            _platformSettings = ImporterOptimization.EffectiveAudioSettings(_audioImporter, context.ImporterPlatform);
            _source = !string.IsNullOrEmpty(context.ImporterPlatform) && _audioImporter.ContainsSampleSettingsOverride(context.ImporterPlatform)
                ? context.DisplayName + " override" : "Default";
            if (refreshMetadata) RefreshMetadata();
        }

        private void RefreshMetadata()
        {
            _importedSize = null;
            _ratio = null;
            _originalSize = null;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath);
            _duration = clip != null ? (float?)clip.length : null;
            _channels = clip != null ? (int?)clip.channels : null;
            _sampleRate = clip != null ? (int?)clip.frequency : null;
            // Read the same preview data used by Unity's AudioImporter inspector for the active platform.
            using (var serializedImporter = new SerializedObject(_audioImporter))
            {
                var originalSize = serializedImporter.FindProperty("m_PreviewData.m_OrigSize");
                var importedSize = serializedImporter.FindProperty("m_PreviewData.m_CompSize");
                if (originalSize != null && originalSize.longValue >= 0)
                    _originalSize = originalSize.longValue;
                if (importedSize == null || importedSize.longValue < 0) return;
                _importedSize = importedSize.longValue;
                if (originalSize != null && originalSize.longValue > 0)
                    _ratio = 100.0 * _importedSize.Value / originalSize.longValue;
            }
        }

        public AudioTreeItem(string name, int depth, int id, string audioPath, AudioImporter audioImporter) : base(name, depth, id)
        {
            if (depth == -1)
                return;

            AudioPath = audioPath;
            AudioName = Path.GetFileName(audioPath);

            _audioImporter = audioImporter;
            Refresh();
        }
    }
}
