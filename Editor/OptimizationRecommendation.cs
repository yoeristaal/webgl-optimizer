using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyGames
{
    internal enum OptimizationAssetKind { Texture, Model, Audio }
    internal enum OptimizationSeverity { High, Medium, Low }
    internal enum OptimizationRule
    {
        TextureCompression, TextureMaxSize, ModelReadWrite, ModelPolygons,
        ModelVertices, ModelCompression, AudioQuality
    }

    internal sealed class OptimizationRecommendation
    {
        public string Path;
        public OptimizationRule Rule;
        public OptimizationSeverity Severity;
        public string Title;
        public string Change;
        public string Explanation;
        public bool Selected = false;
        public string Guid;
        public string Baseline;
        public string ContextKey;
    }

    internal struct OptimizationFacts
    {
        public string Path;
        public OptimizationAssetKind Kind;
        public bool UncompressedTexture;
        public bool AutomaticTextureFormat;
        public int TextureMaxSize;
        public bool ModelReadable;
        public bool OptimizePolygons;
        public bool OptimizeVertices;
        public bool ModelCompressionOff;
        public bool VorbisAudio;
        public float AudioQuality;
        public string PlatformName;
        public string ContextKey;
    }

    internal static class OptimizationRules
    {
        public static List<OptimizationRecommendation> Evaluate(OptimizationFacts facts)
        {
            var result = new List<OptimizationRecommendation>();
            string platform = facts.PlatformName ?? "Active platform";
            if (facts.Kind == OptimizationAssetKind.Texture)
            {
                if (facts.UncompressedTexture && facts.AutomaticTextureFormat)
                    Add(result, facts, OptimizationRule.TextureCompression, OptimizationSeverity.High,
                        "Compress texture for " + platform, platform + " compression: Uncompressed → Normal",
                        $"Compression can reduce texture memory and build size. Check gradients, UI, and transparency for artifacts. Creates a {platform} override if needed.");
                if (facts.TextureMaxSize > 1024)
                    Add(result, facts, OptimizationRule.TextureMaxSize, OptimizationSeverity.Medium,
                        "Review texture resolution", $"{platform} max size: {facts.TextureMaxSize} → 1024",
                        $"A lower resolution can reduce memory and build size. Check the texture at its largest on-screen size before keeping this change. Creates a {platform} override if needed.");
            }
            else if (facts.Kind == OptimizationAssetKind.Model)
            {
                if (facts.ModelReadable)
                    Add(result, facts, OptimizationRule.ModelReadWrite, OptimizationSeverity.High,
                        "Review mesh Read/Write", "Read/Write: Enabled → Disabled",
                        "Removing the CPU copy can save memory. Keep Read/Write enabled if scripts read or modify the mesh or another feature requires CPU access. This setting affects all platforms.");
                if (!facts.OptimizePolygons)
                    Add(result, facts, OptimizationRule.ModelPolygons, OptimizationSeverity.Low,
                        "Optimize mesh polygons", "Optimize polygons: Disabled → Enabled",
                        "Reorders triangles for GPU locality. Check code that depends on triangle order. This setting affects all platforms.");
                if (!facts.OptimizeVertices)
                    Add(result, facts, OptimizationRule.ModelVertices, OptimizationSeverity.Low,
                        "Optimize mesh vertices", "Optimize vertices: Disabled → Enabled",
                        "Reorders vertices for GPU locality. Check code that depends on vertex indices. This setting affects all platforms.");
                if (facts.ModelCompressionOff)
                    Add(result, facts, OptimizationRule.ModelCompression, OptimizationSeverity.Medium,
                        "Review mesh compression", "Mesh compression: Off → Low",
                        "May reduce build size at the cost of vertex precision. Check geometry and animation for artifacts. This setting affects all platforms.");
            }
            else if (facts.VorbisAudio && facts.AudioQuality > 0.7f)
                Add(result, facts, OptimizationRule.AudioQuality, OptimizationSeverity.Medium,
                    "Review audio compression quality", $"{platform} Vorbis quality: {facts.AudioQuality:P0} → 70%",
                    $"Lower quality may reduce download size. Listen for artifacts, especially in dialogue and music. Creates a {platform} override if needed.");
            return result;
        }

        public static List<OptimizationRecommendation> Rank(IEnumerable<OptimizationRecommendation> issues)
        {
            return issues.OrderBy(issue => issue.Severity).ThenBy(issue => issue.Path, StringComparer.Ordinal)
                .ThenBy(issue => issue.Rule).ToList();
        }

        private static void Add(List<OptimizationRecommendation> result, OptimizationFacts facts,
            OptimizationRule rule, OptimizationSeverity severity, string title, string change, string explanation)
        {
            result.Add(new OptimizationRecommendation
            {
                Path = facts.Path, Rule = rule, Severity = severity, Title = title,
                Change = change, Explanation = explanation, ContextKey = facts.ContextKey
            });
        }
    }
}
