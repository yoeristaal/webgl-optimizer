using CrazyGames.TreeLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace CrazyGames.WindowComponents.MaterialOptimizations
{
    internal sealed class MaterialTreeItem : TreeElement
    {
        public Material Material { get; }
        public string AssetPath { get; }
        public double? BuildSize { get; internal set; }
        public string ShaderName { get; }
        public int RenderQueue { get; }
        public int PassCount { get; }
        public bool Instancing { get; }
        public bool DoubleSidedGI { get; }
        public int KeywordCount { get; }
        public string Keywords { get; }
        public int TextureCount { get; }
        public long TextureMemory { get; }

        public MaterialTreeItem(int id, Material material, string path) : base(
            material != null ? material.name : "Root", material != null ? 0 : -1, id)
        {
            if (material == null) return;
            Material = material;
            AssetPath = path;
            ShaderName = material.shader != null ? material.shader.name : "Missing shader";
            RenderQueue = material.renderQueue;
            PassCount = material.passCount;
            Instancing = material.enableInstancing;
            DoubleSidedGI = material.doubleSidedGI;
            var keywords = material.shaderKeywords;
            System.Array.Sort(keywords, System.StringComparer.Ordinal);
            KeywordCount = keywords.Length;
            Keywords = keywords.Length == 0 ? "No enabled keywords." : string.Join(", ", keywords);

            var textures = new HashSet<Texture>();
            long memory = 0;
            bool available = true;
            if (material.shader != null)
            {
                foreach (string property in material.GetTexturePropertyNames())
                {
                    var texture = material.GetTexture(property);
                    if (texture == null || !textures.Add(texture)) continue;
                    long size = Profiler.GetRuntimeMemorySizeLong(texture);
                    if (size <= 0) available = false;
                    else memory += size;
                }
            }
            else available = false;
            TextureCount = textures.Count;
            TextureMemory = available ? memory : -1;
        }
    }
}
