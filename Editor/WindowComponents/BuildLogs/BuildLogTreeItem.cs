using System;
using System.IO;
using CrazyGames.TreeLib;
using UnityEditor;

namespace CrazyOptimizer.Editor.WindowComponents.BuildLogs
{
    public class BuildLogTreeItem : TreeElement
    {
        public readonly float size;
        public readonly string sizeUnit;
        public float sizePercentage { get; internal set; }
        public readonly string filePath;

        public double sizeInBytes
        {
            get
            {
                switch (sizeUnit)
                {
                    case "b":
                        return size;
                    case "kb":
                        return (double)size * 1024;
                    case "mb":
                        return (double)size * 1024 * 1024;
                    case "gb":
                        return (double)size * 1024 * 1024 * 1024;
                    default:
                        throw new Exception("Unknown size unit " + sizeUnit);
                }
            }
        }


        public BuildLogTreeItem(string name, int depth, int id, float size, string sizeUnit, float sizePercentage, string filePath) : base(name, depth, id)
        {
            if (depth == -1)
                return;
            this.size = size;
            this.sizeUnit = sizeUnit;
            this.sizePercentage = sizePercentage;
            this.filePath = filePath;
        }
    }
}