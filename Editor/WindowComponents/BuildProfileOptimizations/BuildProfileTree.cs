using System;
using System.Collections.Generic;
using System.Linq;
using CrazyGames.TreeLib;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CrazyGames.WindowComponents.BuildProfileOptimizations
{
    class BuildProfileTree : TreeViewWithTreeModel<BuildProfileTreeItem>
    {
        public BuildProfileTree(TreeViewState<int> treeViewState, MultiColumnHeader multiColumnHeader, TreeModel<BuildProfileTreeItem> model)
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
                return;
            }

            var sortedColumns = multiColumnHeader.state.sortedColumns;

            if (sortedColumns.Length == 0)
                return;

            var items = rootItem.children.Cast<TreeViewItemData<BuildProfileTreeItem>>().OrderBy(i => i.data.ProfileName);
            var sortedColumnIndex = sortedColumns[0];
            var ascending = multiColumnHeader.IsSortedAscending(sortedColumnIndex);

            switch (sortedColumnIndex)
            {
                case 0:
                    items = items.Order(i => i.data.ProfileName, ascending);
                    break;
                case 1:
                    items = items.Order(i => i.data.PlatformName, ascending);
                    break;
                case 2:
                    items = items.Order(i => i.data.GameName, ascending);
                    break;
                case 3:
                    items = items.Order(i => i.data.FirstScene, ascending);
                    break;
                case 4:
                    items = items.Order(i => i.data.IsActive, ascending);
                    break;
                case 5:
                    items = items.Order(i => i.data.SceneCount, ascending);
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
            var item = (TreeViewItemData<BuildProfileTreeItem>)args.item;

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                CellGUI(args.GetCellRect(i), item, args.GetColumn(i), ref args);
            }
        }

        private void CellGUI(Rect cellRect, TreeViewItemData<BuildProfileTreeItem> item, int column, ref RowGUIArgs args)
        {
            CenterRectUsingSingleLineHeight(ref cellRect);

            switch (column)
            {
                case 0: // Profile Name
                    GUI.Label(cellRect, item.data.ProfileName);
                    break;
                case 1: // Platform
                    GUI.Label(cellRect, item.data.PlatformName);
                    break;
                case 2: // Game Name
                    GUI.Label(cellRect, item.data.GameName);
                    break;
                case 3: // First Scene
                    GUI.Label(cellRect, item.data.FirstScene);
                    break;
                case 4: // Active
                    GUI.Label(cellRect, item.data.IsActive ? "Yes" : "No");
                    break;
                case 5: // Scenes (count)
                    GUI.Label(cellRect, item.data.SceneCount.ToString());
                    break;
                case 6: // Build and Run button
                    if (GUI.Button(cellRect, "Build & Run"))
                    {
                        BuildAndRunProfile(item.data.Profile);
                    }
                    break;
            }
        }

        private void BuildAndRunProfile(BuildProfile profile)
        {
            try
            {
                Debug.Log($"Switching to build profile: {profile.name}");

                // Set this profile as the active profile
                BuildProfile.SetActiveBuildProfile(profile);

                Debug.Log($"Starting Build and Run for profile: {profile.name}");

                // Build and Run using the Build Profiles API
                var buildOptions = new BuildPlayerWithProfileOptions
                {
                    buildProfile = profile,
                    options = BuildOptions.AutoRunPlayer
                };

                var report = BuildPipeline.BuildPlayer(buildOptions);

                if (report.summary.result == BuildResult.Succeeded)
                {
                    Debug.Log($"Build and Run succeeded for profile: {profile.name}");
                }
                else
                {
                    Debug.LogError($"Build and Run failed for profile: {profile.name}. Result: {report.summary.result}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during Build and Run for profile {profile.name}: {e.Message}");
            }
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            base.SelectionChanged(selectedIds);
            var item = treeModel.Find(selectedIds.First());
            if (item?.Profile != null)
            {
                Selection.activeObject = item.Profile;
            }
        }
    }
}