using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ExportSWC.Utils;
using ProjectManager.Controls.TreeView;
using ProjectManager.Projects.AS3;
using System.Linq;
using PluginCore;
using PluginCore.Managers;

namespace ExportSWC
{
    public partial class PluginMain
    {
        private bool _openingLxmlSource;

        private void InjectContextMenuItems(ProjectTreeView tree)
        {
            if (CurrentSwcProject is null)
            {
                return;
            }

            EnsureNotNull(CurrentProjectPath);
            EnsureNotNull(CurrentSwcProject);

            // we're only interested in single items
            if (tree.SelectedNodes.Count == 1)
            {
                var node = tree.SelectedNode;

                if (node.BackingPath.Length <= CurrentProjectPath.FullName.Length)
                {
                    return;
                }

                var nodeRelative = GetRelativePath(CurrentProjectPath.FullName, node.BackingPath);
                nodeRelative ??= node.BackingPath;

                nodeRelative = nodeRelative.ToLower();

                // as3 file
                if (Path.GetExtension(node.BackingPath).ToLower() is ".as" or
                    ".mxml")
                {
                    /* cs3 ignore item */
                    var ignoreCs3 = new ToolStripMenuItem("Exclude from CS3 SWC")
                    {
                        CheckOnClick = true,
                        Tag = node,
                        Checked = CurrentSwcProject.CS3IgnoreClasses.Contains(nodeRelative)
                    };
                    ignoreCs3.CheckedChanged += new EventHandler(IgnoreCs3_CheckedChanged);

                    /* flex ignore item */
                    var ignoreFlex = new ToolStripMenuItem("Exclude from Flex SWC")
                    {
                        CheckOnClick = true,
                        Tag = node,
                        Checked = CurrentSwcProject.FlexIgnoreClasses.Contains(nodeRelative)
                    };
                    ignoreFlex.CheckedChanged += new EventHandler(IgnoreFlex_CheckedChanged);

                    tree.ContextMenuStrip.Items.Add(new ToolStripSeparator());
                    tree.ContextMenuStrip.Items.Add(ignoreCs3);
                    tree.ContextMenuStrip.Items.Add(ignoreFlex);
                }
                // as3 project file
                else if (Path.GetExtension(node.BackingPath).ToLower() == ".as3proj")
                {
                    var compileToSWC = new ToolStripMenuItem("Compile to SWC")
                    {
                        Tag = node
                    };
                    compileToSWC.Click += delegate
                    {
                        var proj = AS3Project.Load(node.BackingPath);

                        _compiler.Build(proj, GetSwcProjectSettings(proj));
                    };

                    var openSwcSettings = new ToolStripMenuItem("SWC Settings")
                    {
                        Tag = node
                    };
                    openSwcSettings.Click += delegate
                    {
                        var proj = AS3Project.Load(node.BackingPath);

                        ConfigureSwcProject(proj, GetSwcProjectSettings(proj));
                    };

                    tree.ContextMenuStrip.Items.Add(new ToolStripSeparator());
                    tree.ContextMenuStrip.Items.Add(compileToSWC);
                    tree.ContextMenuStrip.Items.Add(openSwcSettings);
                }
                else if (Path.GetExtension(node.BackingPath).ToLower() == ExportSWCConstants.SwcConfigFileExentions)
                {
                    var openSource = new ToolStripMenuItem("Open SWC-Project Source")
                    {
                        Tag = node
                    };
                    openSource.Click += delegate
                    {
                        _openingLxmlSource = true;
                        PluginBase.MainForm.OpenEditableDocument(node.BackingPath);
                    };

                    //ToolStripMenuItem openMenuItem = null;
                    try
                    {
                        var projectContextMenu = tree.ContextMenuStrip.Items.OfType<ToolStripItem>()
                            .Select(x => x.Owner)
                            .OfType<ProjectContextMenu>()
                            .FirstOrDefault();

                        var openMenuItem = projectContextMenu.Open;
                        var openMenuItemIndex = tree.ContextMenuStrip.Items.OfType<ToolStripItem>()
                            .Select((x, i) => new { item = x, i })
                            .Where(x => x.item == openMenuItem)
                            .Select(x => (int?)x.i)
                            .FirstOrDefault();

                        var index = openMenuItemIndex is not null ? openMenuItemIndex.Value + 1 : 1;

                        tree.ContextMenuStrip.Items.Insert(index, openSource);
                    }
                    catch (Exception ex)
                    {
                        tree.ContextMenuStrip.Items.Add(openSource);
                    }
                }
            }
        }

        private void IgnoreFlex_CheckedChanged(object sender, EventArgs e)
        {
            EnsureNotNull(CurrentProjectPath);
            EnsureNotNull(CurrentSwcProject);

            var exBtn = (ToolStripMenuItem)sender;
            var node = (GenericNode)exBtn.Tag;

            var nodePath = EnsureNotNull(GetRelativePath(CurrentProjectPath.FullName, node.BackingPath));

            CurrentSwcProject.FlexIgnoreClasses.Remove(nodePath);

            if (exBtn.Checked)
            {
                CurrentSwcProject.FlexIgnoreClasses.Add(nodePath);
                node.ForeColorRequest = Color.DarkGray;
            }
            else
            {
                if (!PathUtils.IsFileIgnored(CurrentProjectPath.FullName, nodePath, CurrentSwcProject.CS3IgnoreClasses))
                {
                    node.ForeColorRequest = Color.Black;
                }
            }

            CurrentSwcProject.Save(CurrentSWCProjectPath!);
        }

        private void IgnoreCs3_CheckedChanged(object sender, EventArgs e)
        {
            EnsureNotNull(CurrentProjectPath);
            EnsureNotNull(CurrentSWCProjectPath);
            EnsureNotNull(CurrentSwcProject);

            var exBtn = (ToolStripMenuItem)sender;
            var node = (GenericNode)exBtn.Tag;
            var nodePath = EnsureNotNull(GetRelativePath(CurrentProjectPath.FullName, node.BackingPath));

            CurrentSwcProject.CS3IgnoreClasses.Remove(nodePath);

            if (exBtn.Checked)
            {
                CurrentSwcProject.CS3IgnoreClasses.Add(nodePath);
                node.ForeColorRequest = Color.DarkGray;
            }
            else
            {
                if (!PathUtils.IsFileIgnored(CurrentProjectPath.FullName, nodePath, CurrentSwcProject.FlexIgnoreClasses))
                {
                    node.ForeColorRequest = Color.Black;
                }
            }

            CurrentSwcProject.Save(CurrentSWCProjectPath);
        }
    }
}
