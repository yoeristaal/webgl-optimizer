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

        private AudioImporter _audioImporter;
        private AudioImporterSampleSettings _platformSettings;
        private string _contextKey, _source;
        private int _revision = -1;
        private double _nextRefresh;

        private void Refresh()
        {
            if (AudioPath == null) return;
            var context = ActivePlatform.Current;
            if (_contextKey == context.Key && _revision == ActivePlatform.Revision && EditorApplication.timeSinceStartup < _nextRefresh) return;
            _contextKey = context.Key;
            _revision = ActivePlatform.Revision;
            _nextRefresh = EditorApplication.timeSinceStartup + 0.25;
            _audioImporter = AssetImporter.GetAtPath(AudioPath) as AudioImporter;
            if (_audioImporter == null) { _source = "Unavailable"; _platformSettings = default; return; }
            _platformSettings = ImporterOptimization.EffectiveAudioSettings(_audioImporter, context.ImporterPlatform);
            _source = !string.IsNullOrEmpty(context.ImporterPlatform) && _audioImporter.ContainsSampleSettingsOverride(context.ImporterPlatform)
                ? context.DisplayName + " override" : "Default";
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
