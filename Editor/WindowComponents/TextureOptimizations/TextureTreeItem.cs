using CrazyGames.TreeLib;
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CrazyGames.WindowComponents.TextureOptimizations
{
    public class TextureTreeItem : TreeElement
    {
        public string TexturePath { get; }
        public string TextureName { get; }
        public long? CompressedSize { get { Refresh(); return _compressedSize; } }

        public int TextureMaxSize { get { Refresh(); return _platformSettings.maxTextureSize; } }
        public int CrunchCompressionQuality { get { Refresh(); return _platformSettings.compressionQuality; } }
        public bool HasCrunchCompression { get { Refresh(); return _platformSettings.crunchedCompression; } }
        public TextureImporterFormat TextureFormat { get { Refresh(); return _platformSettings.format; } }
        public TextureImporterType TextureType { get { Refresh(); return _textureImporter == null ? default : _textureImporter.textureType; } }
        public TextureImporterCompression TextureCompression { get { Refresh(); return _platformSettings.textureCompression; } }
        public string OverrideStatus { get { Refresh(); return _source; } }

        public string TextureCompressionName
        {
            get
            {
                switch (TextureCompression)
                {
                    case TextureImporterCompression.Uncompressed:
                        return "Uncompressed";
                    case TextureImporterCompression.Compressed:
                        return "Normal";
                    case TextureImporterCompression.CompressedHQ:
                        return "High";
                    case TextureImporterCompression.CompressedLQ:
                        return "Low";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private TextureImporter _textureImporter;
        private TextureImporterPlatformSettings _platformSettings = new TextureImporterPlatformSettings();
        private string _contextKey, _source;
        private int _revision = -1;
        private double _nextRefresh;
        private long? _compressedSize;
        // TextureUtil is internal; use the same storage-size query as Unity's texture inspector.
        private static readonly MethodInfo StorageSizeMethod = typeof(Editor).Assembly.GetType("UnityEditor.TextureUtil")
            ?.GetMethod("GetStorageMemorySizeLong", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(Texture) }, null);

        private void Refresh()
        {
            if (TexturePath == null) return;
            var context = ActivePlatform.Current;
            if (_contextKey == context.Key && _revision == ActivePlatform.Revision && EditorApplication.timeSinceStartup < _nextRefresh) return;
            bool refreshSize = _contextKey != context.Key || _revision != ActivePlatform.Revision;
            _contextKey = context.Key;
            _revision = ActivePlatform.Revision;
            _nextRefresh = EditorApplication.timeSinceStartup + 0.25;
            _textureImporter = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (_textureImporter == null) { _source = "Unavailable"; _platformSettings = new TextureImporterPlatformSettings(); _compressedSize = null; return; }
            _platformSettings = ImporterOptimization.EffectiveTextureSettings(_textureImporter, context.ImporterPlatform);
            _source = _platformSettings.overridden ? context.DisplayName + " override" : "Default";
            if (refreshSize || !_compressedSize.HasValue) RefreshCompressedSize();
        }

        private void RefreshCompressedSize()
        {
            _compressedSize = null;
            if (StorageSizeMethod == null || _textureImporter.textureStillNeedsToBeCompressed) return;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture == null) return;
            try
            {
                var size = (long)StorageSizeMethod.Invoke(null, new object[] { texture });
                if (size >= 0) _compressedSize = size;
            }
            catch (TargetInvocationException) { }
            catch (MemberAccessException) { }
        }

        public TextureTreeItem(string name, int depth, int id, string texturePath, TextureImporter textureImporter) : base(name, depth, id)
        {
            if (depth == -1)
                return;

            TexturePath = texturePath;
            TextureName = Path.GetFileName(texturePath);

            _textureImporter = textureImporter;
            Refresh();
        }
    }
}
