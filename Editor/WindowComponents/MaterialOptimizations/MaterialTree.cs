using CrazyGames.TreeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CrazyGames.WindowComponents.MaterialOptimizations
{
    internal sealed class MaterialTree : TreeViewWithTreeModel<MaterialTreeItem>
    {
        public MaterialTree(TreeViewState<int> state, MultiColumnHeader header, TreeModel<MaterialTreeItem> model)
            : base(state, header, model)
        {
            showBorder = true;
            showAlternatingRowBackgrounds = true;
            header.sortingChanged += _ => Reload();
            Reload();
        }

        protected override IList<TreeViewItem<int>> BuildRows(TreeViewItem<int> root)
        {
            var rows = base.BuildRows(root);
            int column = multiColumnHeader.sortedColumnIndex;
            if (column < 0 || rows.Count < 2) return rows;
            Func<TreeViewItem<int>, IComparable> key = row =>
                SortValue(((TreeViewItemData<MaterialTreeItem>)row).data, column);
            var sorted = multiColumnHeader.IsSortedAscending(column)
                ? rows.OrderBy(key).ThenBy(row => row.displayName, StringComparer.OrdinalIgnoreCase).ToList()
                : rows.OrderByDescending(key).ThenBy(row => row.displayName, StringComparer.OrdinalIgnoreCase).ToList();
            rows.Clear();
            foreach (var row in sorted) rows.Add(row);
            SetupParentsAndChildrenFromDepths(root, rows);
            return rows;
        }

        private static IComparable SortValue(MaterialTreeItem item, int column)
        {
            switch (column)
            {
                case 0: return item.name;
                case 1: return item.ShaderName;
                case 2: return item.RenderQueue;
                case 3: return item.PassCount;
                case 4: return item.Instancing;
                case 5: return item.KeywordCount;
                case 6: return item.TextureCount;
                case 7: return item.TextureMemory;
                case 8: return item.DoubleSidedGI;
                case 10: return item.BuildSize ?? -1;
                default: return item.AssetPath;
            }
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = ((TreeViewItemData<MaterialTreeItem>)args.item).data;
            for (int i = 0; i < args.GetNumVisibleColumns(); i++)
            {
                var rect = args.GetCellRect(i);
                CenterRectUsingSingleLineHeight(ref rect);
                int column = args.GetColumn(i);
                string value;
                switch (column)
                {
                    case 0: value = item.name; break;
                    case 1: value = item.ShaderName; break;
                    case 2: value = item.RenderQueue.ToString(); break;
                    case 3: value = item.PassCount.ToString(); break;
                    case 4: value = item.Instancing ? "yes" : "no"; break;
                    case 5: value = item.KeywordCount.ToString(); break;
                    case 6: value = item.TextureCount.ToString(); break;
                    case 7: value = item.TextureMemory < 0 ? "N/A" : EditorUtility.FormatBytes(item.TextureMemory); break;
                    case 8: value = item.DoubleSidedGI ? "yes" : "no"; break;
                    case 10: value = item.BuildSize.HasValue ? EditorUtility.FormatBytes((long)item.BuildSize.Value) : "N/A"; break;
                    default: value = item.AssetPath; break;
                }
                GUI.Label(rect, new GUIContent(value, column == 5 ? item.Keywords : column == 10 ? BuildAssetReport.SizeTooltip : item.AssetPath));
            }
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            base.SelectionChanged(selectedIds);
            if (selectedIds == null || selectedIds.Count == 0) return;
            var item = treeModel.Find(selectedIds[0]);
            if (item?.Material != null) Selection.activeObject = item.Material;
        }

        protected override void DoubleClickedItem(int id)
        {
            var item = treeModel.Find(id);
            if (item?.Material != null) EditorGUIUtility.PingObject(item.Material);
        }

        protected override bool CanStartDrag(CanStartDragArgs args) => false;
    }
}
