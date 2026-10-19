using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrazyGames.TreeLib;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CrazyGames.WindowComponents.AudioOptimizations
{
    class AudioTree : TreeViewWithTreeModel<AudioTreeItem>
    {
        public AudioTree(TreeViewState<int> treeViewState, MultiColumnHeader multiColumnHeader, TreeModel<AudioTreeItem> model)
            : base(treeViewState, multiColumnHeader, model)
        {
            showBorder = true;
            showAlternatingRowBackgrounds = true;
            multiColumnHeader.sortingChanged += OnSortingChanged;
            Reload();
        }

        void SortIfNeeded(TreeViewItem<int> root, IList<TreeViewItem<int>> rows)
        {
            if (rows.Count <= 1)
                return;

            if (multiColumnHeader.sortedColumnIndex == -1)
            {
                return; // No column to sort for (just use the order the data are in)
            }

            var sortedColumns = multiColumnHeader.state.sortedColumns;

            if (sortedColumns.Length == 0)
                return;

            var items = rootItem.children.Cast<TreeViewItemData<AudioTreeItem>>().OrderBy(i => i.data.AudioName);
            var sortedColumnIndex = sortedColumns[0];
            var ascending = multiColumnHeader.IsSortedAscending(sortedColumnIndex);
            switch (sortedColumnIndex)
            {
                case 0:
                    items = items.Order(i => i.data.AudioName, ascending);
                    break;
                case 1:
                    items = items.Order(i => i.data.LoadType, ascending);
                    break;
                case 2:
                    items = items.Order(i => i.data.Quality, ascending);
                    break;
                case 3:
                    items = items.Order(i => i.data.SettingsSource, ascending);
                    break;
                case 4:
                    items = items.Order(i => i.data.CompressionFormat, ascending);
                    break;
                case 5:
                    items = items.Order(i => i.data.ImportedSize, ascending);
                    break;
                case 6:
                    items = items.Order(i => i.data.Ratio, ascending);
                    break;
                case 7:
                    items = items.Order(i => i.data.OriginalSize, ascending);
                    break;
                case 8:
                    items = items.Order(i => i.data.Duration, ascending);
                    break;
                case 9:
                    items = items.Order(i => i.data.Channels, ascending);
                    break;
                case 10:
                    items = items.Order(i => i.data.SampleRate, ascending);
                    break;
                case 11:
                    items = items.Order(i => i.data.ForceToMono, ascending);
                    break;
                case 12:
                    items = items.Order(i => i.data.PreloadAudioData, ascending);
                    break;
                case 13:
                    items = items.Order(i => i.data.LoadInBackground, ascending);
                    break;
            }

            rootItem.children = items.Cast<TreeViewItem<int>>().ToList();
            TreeToList(root, rows);
            Repaint();
        }

        public static void TreeToList(TreeViewItem<int> root, IList<TreeViewItem<int>> result)
        {
            if (root == null)
                throw new NullReferenceException("root");
            if (result == null)
                throw new NullReferenceException("result");

            result.Clear();

            if (root.children == null)
                return;

            Stack<TreeViewItem<int>> stack = new Stack<TreeViewItem<int>>();
            for (int i = root.children.Count - 1; i >= 0; i--)
                stack.Push(root.children[i]);

            while (stack.Count > 0)
            {
                TreeViewItem<int> current = stack.Pop();
                result.Add(current);

                if (current.hasChildren && current.children[0] != null)
                {
                    for (int i = current.children.Count - 1; i >= 0; i--)
                    {
                        stack.Push(current.children[i]);
                    }
                }
            }
        }

        void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        {
            SortIfNeeded(rootItem, GetRows());
        }

        protected override IList<TreeViewItem<int>> BuildRows(TreeViewItem<int> root)
        {
            var rows = base.BuildRows(root);
            SortIfNeeded(root, rows);
            return rows;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = (TreeViewItemData<AudioTreeItem>)args.item;

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                CellGUI(args.GetCellRect(i), item, args.GetColumn(i), ref args);
            }
        }

        private void CellGUI(Rect cellRect, TreeViewItemData<AudioTreeItem> item, int column, ref RowGUIArgs args)
        {
            CenterRectUsingSingleLineHeight(ref cellRect);
            switch (column)
            {
                case 0:
                    GUI.Label(cellRect, new GUIContent(item.data.AudioName, item.data.AudioPath));
                    break;
                case 1:
                    GUI.Label(cellRect, item.data.LoadType);
                    break;
                case 2:
                    GUI.Label(cellRect, item.data.Quality.ToString());
                    break;
                case 3:
                    GUI.Label(cellRect, item.data.SettingsSource);
                    break;
                case 4:
                    GUI.Label(cellRect, item.data.CompressionFormat);
                    break;
                case 5:
                    var importedSize = item.data.ImportedSize;
                    GUI.Label(cellRect, importedSize.HasValue ? EditorUtility.FormatBytes(importedSize.Value) : "N/A");
                    break;
                case 6:
                    var ratio = item.data.Ratio;
                    GUI.Label(cellRect, ratio.HasValue ? ratio.Value.ToString("0.00", CultureInfo.InvariantCulture) + "%" : "N/A");
                    break;
                case 7:
                    var originalSize = item.data.OriginalSize;
                    GUI.Label(cellRect, originalSize.HasValue ? EditorUtility.FormatBytes(originalSize.Value) : "N/A");
                    break;
                case 8:
                    var duration = item.data.Duration;
                    GUI.Label(cellRect, duration.HasValue ? duration.Value.ToString("0.00", CultureInfo.InvariantCulture) + " s" : "N/A");
                    break;
                case 9:
                    var channels = item.data.Channels;
                    GUI.Label(cellRect, channels == 1 ? "Mono" : channels == 2 ? "Stereo" : channels?.ToString() ?? "N/A");
                    break;
                case 10:
                    var sampleRate = item.data.SampleRate;
                    GUI.Label(cellRect, sampleRate.HasValue ? (sampleRate.Value / 1000.0).ToString("0.###", CultureInfo.InvariantCulture) + " kHz" : "N/A");
                    break;
                case 11:
                    GUI.Label(cellRect, FormatFlag(item.data.ForceToMono));
                    break;
                case 12:
                    GUI.Label(cellRect, FormatFlag(item.data.PreloadAudioData));
                    break;
                case 13:
                    GUI.Label(cellRect, FormatFlag(item.data.LoadInBackground));
                    break;
            }
        }

        private static string FormatFlag(bool? value) => value.HasValue ? (value.Value ? "Yes" : "No") : "N/A";

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            base.SelectionChanged(selectedIds);
            if (selectedIds == null || selectedIds.Count == 0)
                return;
            var item = treeModel.Find(selectedIds[0]);
            if (item == null)
                return;
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(item.AudioPath);
        }
    }
}
