/*
 * TreeListView - A listview that can show a tree of objects in a column
 *
 * Author: Phillip Piper
 * Date: 23/09/2008 11:15 AM
 *
 * Change log:
 * 2011-04-20  JPP  - Added ExpandedObjects property and RebuildAll() method.
 * 2011-04-09  JPP  - Added Expanding, Collapsing, Expanded and Collapsed events.
 *                    The ..ing events are cancellable. These are only fired in response
 *                    to user actions.
 * v2.4.1
 * 2010-06-15  JPP  - Fixed bug in Tree.RemoveObjects() which resulted in removed objects
 *                    being reported as still existing.
 * v2.3
 * 2009-09-01  JPP  - Fixed off-by-one error that was messing up hit detection
 * 2009-08-27  JPP  - Fixed bug when dragging a node from one place to another in the tree
 * v2.2.1
 * 2009-07-14  JPP  - Clicks to the left of the expander in tree cells are now ignored.
 * v2.2
 * 2009-05-12  JPP  - Added tree traverse operations: GetParent and GetChildren.
 *                  - Added DiscardAllState() to completely reset the TreeListView.
 * 2009-05-10  JPP  - Removed all unsafe code
 * 2009-05-09  JPP  - Fixed bug where any command (Expand/Collapse/Refresh) on a model
 *                    object that was once visible but that is currently in a collapsed branch
 *                    would cause the control to crash.
 * 2009-05-07  JPP  - Fixed bug where RefreshObjects() would fail when none of the given
 *                    objects were present/visible.
 * 2009-04-20  JPP  - Fixed bug where calling Expand() on an already expanded branch confused
 *                    the display of the children (SF#2499313)
 * 2009-03-06  JPP  - Calculate edit rectangle on column 0 more accurately
 * v2.1
 * 2009-02-24  JPP  - All commands now work when the list is empty (SF #2631054)
 *                  - TreeListViews can now be printed with ListViewPrinter
 * 2009-01-27  JPP  - Changed to use new Renderer and HitTest scheme
 * 2009-01-22  JPP  - Added RevealAfterExpand property. If this is true (the default),
 *                    after expanding a branch, the control scrolls to reveal as much of the
 *                    expanded branch as possible.
 * 2009-01-13  JPP  - Changed TreeRenderer to work with visual styles are disabled
 * v2.0.1
 * 2009-01-07  JPP  - Made all public and protected methods virtual 
 *                  - Changed some classes from 'internal' to 'protected' so that they
 *                    can be accessed by subclasses of TreeListView.
 * 2008-12-22  JPP  - Added UseWaitCursorWhenExpanding property
 *                  - Made TreeRenderer public so that it can be subclassed
 *                  - Added LinePen property to TreeRenderer to allow the connection drawing 
 *                    pen to be changed 
 *                  - Fixed some rendering issues where the text highlight rect was miscalculated
 *                  - Fixed connection line problem when there is only a single root
 * v2.0
 * 2008-12-10  JPP  - Expand/collapse with mouse now works when there is no SmallImageList.
 * 2008-12-01  JPP  - Search-by-typing now works.
 * 2008-11-26  JPP  - Corrected calculation of expand/collapse icon (SF#2338819)
 *                  - Fixed ugliness with dotted lines in renderer (SF#2332889)
 *                  - Fixed problem with custom selection colors (SF#2338805)
 * 2008-11-19  JPP  - Expand/collapse now preserve the selection -- more or less :)
 *                  - Overrode RefreshObjects() to rebuild the given objects and their children
 * 2008-11-05  JPP  - Added ExpandAll() and CollapseAll() commands
 *                  - CanExpand is no longer cached
 *                  - Renamed InitialBranches to RootModels since it deals with model objects
 * 2008-09-23  JPP  Initial version
 *
 * TO DO:
 * 2008-12-10  If the TreeListView doesn't have a small image list, checkboxes do not work.
 *             [Is this still the case? 2009/01/27]
 * 2008-10-19  Can we remove the need to ownerdraw the tree view?
 *             If tree does not have checkboxes, we could use the state image
 *             to show the expand/collapse icon. If the tree has check boxes,
 *             it has to be owner drawn.
 * 
 * Copyright (C) 2006-2008 Phillip Piper
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 *
 * If you wish to use this code in a closed source application, please contact phillip_piper@bigfoot.com.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace BrightIdeasSoftware
{
    /// <summary>
    /// A TreeListView combines an expandable tree structure with list view columns.
    /// </summary>
    /// <remarks>
    /// <para>To support tree operations, two delegates must be provided:</para>
    /// <list type="table">
    /// <item>
    /// <term>
    /// CanExpandGetter
    /// </term> 
    /// <description>
    /// This delegate must accept a model object and return a boolean indicating
    /// if that model should be expandable. 
    /// </description>
    /// </item>
    /// <item>
    /// <term>
    /// ChildrenGetter
    /// </term> 
    /// <description>
    /// This delegate must accept a model object and return an IEnumerable of model
    /// objects that will be displayed as children of the parent model. This delegate will only be called
    /// for a model object if the CanExpandGetter has already returned true for that model.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// The top level branches of the tree are set via the Roots property. SetObjects(), AddObjects() 
    /// and RemoveObjects() are interpreted as operations on this collection of roots.
    /// </para>
    /// <para>
    /// To add new children to an existing branch, make changes to your model objects and then
    /// call RefreshObject() on the parent.
    /// </para>
    /// <para>The tree must be a directed acyclic graph -- no cycles are allowed. Put more mundanely, 
    /// each model object must appear only once in the tree. If the same model object appears in two
    /// places in the tree, the control will become confused.</para>
    /// </remarks>
    public partial class TreeListView : VirtualObjectListView
    {
        /// <summary>
        /// Make a default TreeListView
        /// </summary>
        public TreeListView()
        {
            TreeModel = new Tree(this);
            OwnerDraw = true;
            View = View.Details;

            VirtualListDataSource = TreeModel;
            TreeColumnRenderer = new TreeRenderer();

            // This improves hit detection even if we don't have any state image
            StateImageList = new ImageList();
        }

        //------------------------------------------------------------------------------------------
        // Properties

        /// <summary>
        /// This is the delegate that will be used to decide if a model object can be expanded.
        /// </summary>
        [Browsable(false),
         DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public virtual CanExpandGetterDelegate CanExpandGetter
        {
            get { return TreeModel.CanExpandGetter; }
            set { TreeModel.CanExpandGetter = value; }
        }

        /// <summary>
        /// Gets whether or not this listview is capabale of showing groups
        /// </summary>
        [Browsable(false)]
        public override bool CanShowGroups
        {
            get { return false; }
        }

        /// <summary>
        /// This is the delegate that will be used to fetch the children of a model object
        /// </summary>
        /// <remarks>This delegate will only be called if the CanExpand delegate has 
        /// returned true for the model object.</remarks>
        [Browsable(false),
         DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public virtual ChildrenGetterDelegate ChildrenGetter
        {
            get { return TreeModel.ChildrenGetter; }
            set { TreeModel.ChildrenGetter = value; }
        }

        /// <summary>
        /// Gets or sets the model objects that are expanded.
        /// </summary>
        /// <remarks>
        /// <para>This can be used to expand model objects before they are seen.</para>
        /// <para>
        /// Setting this does *not* force the control to rebuild
        /// its display. You need to call RebuildAll(true).
        /// </para>
        /// </remarks>
        [Browsable(false),
         DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IEnumerable ExpandedObjects
        {
            get { return TreeModel.mapObjectToExpanded.Keys; }
            set
            {
                TreeModel.mapObjectToExpanded.Clear();
                foreach (object x in value)
                    TreeModel.SetModelExpanded(x, true);
            }
        }

        /// <summary>
        /// Gets or  sets the filter that is applied to our whole list of objects.
        /// TreeListViews do not currently support whole list filters.
        /// </summary>
        [Browsable(false),
         DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override IListFilter ListFilter
        {
            get { return null; }
            set { Debug.Assert(value == null, "TreeListView do not support ListFilters"); }
        }

        /// <summary>
        /// Gets or sets the collection of root objects of the tree
        /// </summary>
        [Browsable(false),
         DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override IEnumerable Objects
        {
            get { return Roots; }
            set { Roots = value; }
        }

        /// <summary>
        /// After expanding a branch, should the TreeListView attempts to show as much of the 
        /// revealed descendents as possible.
        /// </summary>
        [Category("ObjectListView"),
         Description("Should the parent of an expand subtree be scrolled to the top revealing the children?"),
         DefaultValue(true)]
        public bool RevealAfterExpand
        {
            get { return revealAfterExpand; }
            set { revealAfterExpand = value; }
        }

        private bool revealAfterExpand = true;

        /// <summary>
        /// The model objects that form the top level branches of the tree.
        /// </summary>
        /// <remarks>Setting this does <b>NOT</b> reset the state of the control.
        /// In particular, it does not collapse branches.</remarks>
        [Browsable(false),
         DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public virtual IEnumerable Roots
        {
            get { return TreeModel.RootObjects; }
            set
            {
                // Make sure that column 0 is showing a tree
                if (Columns.Count > 0)
                {
                    OLVColumn columnZero = GetColumn(0);
                    if (!(columnZero.Renderer is TreeRenderer))
                        columnZero.Renderer = TreeColumnRenderer;

                    columnZero.WordWrap = columnZero.WordWrap;
                }
                if (value == null)
                    TreeModel.RootObjects = new ArrayList();
                else
                    TreeModel.RootObjects = value;
                UpdateVirtualListSize();
            }
        }

        /// <summary>
        /// Gets or sets the renderer that will be used to draw the tree structure.
        /// Setting this to null resets the renderer to default.
        /// </summary>
        [Browsable(false),
         DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public virtual TreeRenderer TreeColumnRenderer
        {
            get { return treeRenderer; }
            set
            {
                treeRenderer = value ?? new TreeRenderer();
                if (Columns.Count > 0)
                    GetColumn(0).Renderer = treeRenderer;
            }
        }

        private TreeRenderer treeRenderer;

        /// <summary>
        /// Should a wait cursor be shown when a branch is being expanded?
        /// </summary>
        /// <remarks>When this is true, the wait cursor will be shown whilst the children of the 
        /// branch are being fetched. If the children of the branch have already been cached, 
        /// the cursor will not change.</remarks>
        [Category("ObjectListView"),
         Description("Should a wait cursor be shown when a branch is being expaned?"),
         DefaultValue(true)]
        public virtual bool UseWaitCursorWhenExpanding
        {
            get { return useWaitCursorWhenExpanding; }
            set { useWaitCursorWhenExpanding = value; }
        }

        private bool useWaitCursorWhenExpanding = true;

        /// <summary>
        /// The model that is used to manage the tree structure
        /// </summary>
        protected Tree TreeModel { get; set; }

        //------------------------------------------------------------------------------------------
        // Accessing

        /// <summary>
        /// Return true if the branch at the given model is expanded
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public virtual bool IsExpanded(Object model)
        {
            Branch br = TreeModel.GetBranch(model);
            return (br != null && br.IsExpanded);
        }

        //------------------------------------------------------------------------------------------
        // Commands

        /// <summary>
        /// Collapse the subtree underneath the given model
        /// </summary>
        /// <param name="model"></param>
        public virtual void Collapse(Object model)
        {
            if (GetItemCount() == 0)
                return;
            IList selection = SelectedObjects;
            int index = TreeModel.Collapse(model);
            if (index >= 0)
            {
                UpdateVirtualListSize();
                SelectedObjects = selection;
                RedrawItems(index, GetItemCount() - 1, false);
            }
        }

        /// <summary>
        /// Collapse all subtrees within this control
        /// </summary>
        public virtual void CollapseAll()
        {
            if (GetItemCount() == 0)
                return;
            IList selection = SelectedObjects;
            int index = TreeModel.CollapseAll();
            if (index >= 0)
            {
                UpdateVirtualListSize();
                SelectedObjects = selection;
                RedrawItems(index, GetItemCount() - 1, false);
            }
        }

        /// <summary>
        /// Collapse all roots and forget everything we know about all models
        /// </summary>
        public virtual void DiscardAllState()
        {
            RebuildAll(false);
        }

        /// <summary>
        /// Completely rebuild the tree structure
        /// </summary>
        /// <param name="preserveState">If true, the control will try to preserve selection and expansion</param>
        public virtual void RebuildAll(bool preserveState)
        {
            RebuildAll(
                preserveState ? SelectedObjects : null,
                preserveState ? ExpandedObjects : null);
        }

        /// <summary>
        /// Completely rebuild the tree structure
        /// </summary>
        /// <param name="selected">If not null, this list of objects will be selected after the tree is rebuilt</param>
        /// <param name="expanded">If not null, this collection of objects will be expanded after the tree is rebuilt</param>
        protected virtual void RebuildAll(IList selected, IEnumerable expanded)
        {
            try
            {
                BeginUpdate();
                // Remember the bits of info we don't want to forget (anyone ever see Memento?)
                IEnumerable roots = Roots;
                CanExpandGetterDelegate canExpand = CanExpandGetter;
                ChildrenGetterDelegate childrenGetter = ChildrenGetter;

                // Give ourselves a new data structure
                TreeModel = new Tree(this);
                VirtualListDataSource = TreeModel;

                // Put back the bits we didn't want to forget
                CanExpandGetter = canExpand;
                ChildrenGetter = childrenGetter;
                if (expanded != null)
                    ExpandedObjects = expanded;
                Roots = roots;
                if (selected != null)
                    SelectedObjects = selected;
            }
            finally
            {
                EndUpdate();
            }
        }

        /// <summary>
        /// Expand the subtree underneath the given model object
        /// </summary>
        /// <param name="model"></param>
        public virtual void Expand(Object model)
        {
            if (GetItemCount() == 0)
                return;

            // Remember the selection so we can put it back later
            IList selection = SelectedObjects;

            // Expand the model first
            int index = TreeModel.Expand(model);
            if (index < 0)
                return;

            // Update the size of the list and restore the selection
            UpdateVirtualListSize();
            SelectedObjects = selection;

            // Redraw the items that were changed by the expand operation
            RedrawItems(index, GetItemCount() - 1, false);

            if (RevealAfterExpand && index > 0)
            {
                // TODO: This should be a separate method
                BeginUpdate();
                try
                {
                    int countPerPage = NativeMethods.GetCountPerPage(this);
                    int descedentCount = TreeModel.GetVisibleDescendentCount(model);
                    if (descedentCount < countPerPage)
                        EnsureVisible(index + descedentCount);
                    else
                        TopItemIndex = index;
                }
                finally
                {
                    EndUpdate();
                }
            }
        }

        /// <summary>
        /// Expand all the branches within this tree recursively.
        /// </summary>
        /// <remarks>Be careful: this method could take a long time for large trees.</remarks>
        public virtual void ExpandAll()
        {
            if (GetItemCount() == 0)
                return;
            IList selection = SelectedObjects;
            int index = TreeModel.ExpandAll();
            if (index >= 0)
            {
                UpdateVirtualListSize();
                SelectedObjects = selection;
                RedrawItems(index, GetItemCount() - 1, false);
            }
        }

        /// <summary>
        /// Update the rows that are showing the given objects
        /// </summary>
        public override void RefreshObjects(IList modelObjects)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker) delegate { RefreshObjects(modelObjects); });
                return;
            }
            // There is no point in refreshing anything if the list is empty
            if (GetItemCount() == 0)
                return;

            // Remember the selection so we can put it back later
            IList selection = SelectedObjects;

            // Refresh each object, remembering where the first update occured
            int firstChange = Int32.MaxValue;
            foreach (Object model in modelObjects)
            {
                if (model != null)
                {
                    int index = TreeModel.RebuildChildren(model);
                    if (index >= 0)
                        firstChange = Math.Min(firstChange, index);
                }
            }

            // If we didn't refresh any objects, don't do anything else
            if (firstChange >= GetItemCount())
                return;

            ClearCachedInfo();
            UpdateVirtualListSize();
            SelectedObjects = selection;

            // Redraw everything from the first update to the end of the list
            RedrawItems(firstChange, GetItemCount() - 1, false);
        }

        /// <summary>
        /// Toggle the expanded state of the branch at the given model object
        /// </summary>
        /// <param name="model"></param>
        public virtual void ToggleExpansion(Object model)
        {
            OLVListItem item = ModelToItem(model);
            if (IsExpanded(model))
            {
                var args = new TreeBranchCollapsingEventArgs(model, item);
                OnCollapsing(args);
                if (!args.Canceled)
                {
                    Collapse(model);
                    OnCollapsed(new TreeBranchCollapsedEventArgs(model, item));
                }
            }
            else
            {
                var args = new TreeBranchExpandingEventArgs(model, item);
                OnExpanding(args);
                if (!args.Canceled)
                {
                    Expand(model);
                    OnExpanded(new TreeBranchExpandedEventArgs(model, item));
                }
            }
        }

        //------------------------------------------------------------------------------------------
        // Commands - Tree traversal

        /// <summary>
        /// Return the model object that is the parent of the given model object.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        /// <remarks>The given model must have already been seen in the tree.</remarks>
        public virtual Object GetParent(Object model)
        {
            Branch br = TreeModel.GetBranch(model);
            if (br == null || br.ParentBranch == null)
                return null;
            else
                return br.ParentBranch.Model;
        }

        /// <summary>
        /// Return the collection of model objects that are the children of the 
        /// given model.
        /// </summary>
        /// <param name="model"></param>
        /// <remarks>The given model must have already been seen in the tree and
        /// must be expandable</remarks>
        public virtual IEnumerable GetChildren(Object model)
        {
            Branch br = TreeModel.GetBranch(model);
            if (br == null || !br.CanExpand)
                return new ArrayList();
            else
                return br.Children;
        }

        //------------------------------------------------------------------------------------------
        // Delegates

        /// <summary>
        /// Delegates of this type are use to decide if the given model object can be expanded
        /// </summary>
        /// <param name="model">The model under consideration</param>
        /// <returns>Can the given model be expanded?</returns>
        public delegate bool CanExpandGetterDelegate(Object model);

        /// <summary>
        /// Delegates of this type are used to fetch the children of the given model object
        /// </summary>
        /// <param name="model">The parent whose children should be fetched</param>
        /// <returns>An enumerable over the children</returns>
        public delegate IEnumerable ChildrenGetterDelegate(Object model);

        //------------------------------------------------------------------------------------------

        #region Implementation

        /// <summary>
        /// Handle a left button down event
        /// </summary>
        /// <param name="hti"></param>
        /// <returns></returns>
        protected override bool ProcessLButtonDown(OlvListViewHitTestInfo hti)
        {
            // Did they click in the expander?
            if (hti.HitTestLocation == HitTestLocation.ExpandButton)
            {
                PossibleFinishCellEditing();
                ToggleExpansion(hti.RowObject);
                return true;
            }

            return base.ProcessLButtonDown(hti);
        }

        /// <summary>
        /// Create a OLVListItem for given row index
        /// </summary>
        /// <param name="itemIndex">The index of the row that is needed</param>
        /// <returns>An OLVListItem</returns>
        /// <remarks>This differs from the base method by also setting up the IndentCount property.</remarks>
        public override OLVListItem MakeListViewItem(int itemIndex)
        {
            OLVListItem olvItem = base.MakeListViewItem(itemIndex);
            Branch br = TreeModel.GetBranch(olvItem.RowObject);
            if (br != null)
                olvItem.IndentCount = br.Level - 1;
            return olvItem;
        }

        #endregion

        //------------------------------------------------------------------------------------------

        #region Event handlers

        /// <summary>
        /// Decide if the given key event should be handled as a normal key input to the control?
        /// </summary>
        /// <param name="keyData"></param>
        /// <returns></returns>
        protected override bool IsInputKey(Keys keyData)
        {
            // We want to handle Left and Right keys within the control
            if (((keyData & Keys.KeyCode) == Keys.Left) || ((keyData & Keys.KeyCode) == Keys.Right))
            {
                return true;
            }
            else
                return base.IsInputKey(keyData);
        }

        /// <summary>
        /// Handle the keyboard input to mimic a TreeView.
        /// </summary>
        /// <param name="e"></param>
        /// <returns>Was the key press handled?</returns>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            var focused = FocusedItem as OLVListItem;
            if (focused == null)
            {
                base.OnKeyDown(e);
                return;
            }

            Object modelObject = focused.RowObject;
            Branch br = TreeModel.GetBranch(modelObject);

            switch (e.KeyCode)
            {
                case Keys.Left:
                    // If the branch is expanded, collapse it. If it's collapsed,
                    // select the parent of the branch.
                    if (br.IsExpanded)
                        Collapse(modelObject);
                    else
                    {
                        if (br.ParentBranch != null && br.ParentBranch.Model != null)
                            SelectObject(br.ParentBranch.Model, true);
                    }
                    e.Handled = true;
                    break;

                case Keys.Right:
                    // If the branch is expanded, select the first child.
                    // If it isn't expanded and can be, expand it.
                    if (br.IsExpanded)
                    {
                        List<Branch> filtered = br.FilteredChildBranches;
                        if (filtered.Count > 0)
                            SelectObject(filtered[0].Model, true);
                    }
                    else
                    {
                        if (br.CanExpand)
                            Expand(modelObject);
                    }
                    e.Handled = true;
                    break;
            }

            base.OnKeyDown(e);
        }

        #endregion

        //------------------------------------------------------------------------------------------
        // Support classes

        /// <summary>
        /// A Tree object represents a tree structure data model that supports both 
        /// tree and flat list operations as well as fast access to branches.
        /// </summary>
        public class Tree : IVirtualListDataSource, IFilterableDataSource
        {
            /// <summary>
            /// Create a Tree
            /// </summary>
            /// <param name="treeView"></param>
            public Tree(TreeListView treeView)
            {
                this.treeView = treeView;
                trunk = new Branch(null, this, null);
                trunk.IsExpanded = true;
            }

            //------------------------------------------------------------------------------------------
            // Properties

            /// <summary>
            /// This is the delegate that will be used to decide if a model object can be expanded.
            /// </summary>
            public CanExpandGetterDelegate CanExpandGetter { get; set; }

            /// <summary>
            /// This is the delegate that will be used to fetch the children of a model object
            /// </summary>
            /// <remarks>This delegate will only be called if the CanExpand delegate has 
            /// returned true for the model object.</remarks>
            public ChildrenGetterDelegate ChildrenGetter { get; set; }

            /// <summary>
            /// Get or return the top level model objects in the tree
            /// </summary>
            public IEnumerable RootObjects
            {
                get { return trunk.Children; }
                set
                {
                    bool anyOld = trunk.Children.Cast<object>().Any();
                    bool anyNew = value.Cast<object>().Any();
                    if (anyOld && anyNew)
                    {
                        var existing = new Hashtable();
                        foreach (var child in trunk.Children)
                            existing.Add(child, child);
                        foreach (var child in value.Cast<object>().Where(child => !existing.Contains(child)))
                            RemoveBranch(child);
                    }
                    else if (anyOld)
                    {
                        this.mapObjectToBranch.Clear();
                        this.mapObjectToExpanded.Clear();
                        this.mapObjectToIndex.Clear();
                    }
                    trunk.Children = value;
                    foreach (Branch br in trunk.ChildBranches)
                        br.RefreshChildren();
                    RebuildList();
                }
            }

            /// <summary>
            /// What tree view is this Tree the model for?
            /// </summary>
            public TreeListView TreeView
            {
                get { return treeView; }
            }

            //------------------------------------------------------------------------------------------
            // Commands

            /// <summary>
            /// Collapse the subtree underneath the given model
            /// </summary>
            /// <param name="model">The model to be collapsed. If the model isn't in the tree,
            /// or if it is already collapsed, the command does nothing.</param>
            /// <returns>The index of the model in flat list version of the tree</returns>
            public virtual int Collapse(Object model)
            {
                Branch br = GetBranch(model);
                if (br == null || !br.IsExpanded)
                    return -1;

                // Remember that the branch is collapsed, even if it's currently not visible
                if (!br.Visible)
                {
                    br.Collapse();
                    return -1;
                }

                int count = br.NumberVisibleDescendents;
                br.Collapse();

                // Remove the visible descendents from after the branch itself
                int index = GetObjectIndex(model);
                objectList.RemoveRange(index + 1, count);
                RebuildObjectMap(index + 1);
                return index;
            }

            /// <summary>
            /// Collapse all branches in this tree
            /// </summary>
            /// <returns>Return the index of the first root that was not collapsed</returns>
            public virtual int CollapseAll()
            {
                foreach (Branch br in trunk.ChildBranches)
                {
                    if (br.IsExpanded)
                        br.Collapse();
                }
                RebuildList();
                return 0;
            }

            /// <summary>
            /// Expand the subtree underneath the given model object
            /// </summary>
            /// <param name="model">The model to be expanded.</param> 
            /// <returns>The index of the model in flat list version of the tree</returns>
            /// <remarks>
            /// If the model isn't in the tree,
            /// if it cannot be expanded or if it is already expanded, the command does nothing.
            /// </remarks>
            public virtual int Expand(Object model)
            {
                Branch br = GetBranch(model);
                if (br == null || !br.CanExpand || br.IsExpanded)
                    return -1;

                // Remember that the branch is expanded, even if it's currently not visible
                if (!br.Visible)
                {
                    br.Expand();
                    return -1;
                }

                int index = GetObjectIndex(model);
                InsertChildren(br, index + 1);
                return index;
            }

            /// <summary>
            /// Expand all branches in this tree
            /// </summary>
            /// <returns>Return the index of the first branch that was expanded</returns>
            public virtual int ExpandAll()
            {
                trunk.ExpandAll();
                Sort(lastSortColumn, lastSortOrder);
                return 0;
            }

            /// <summary>
            /// Return the Branch object that represents the given model in the tree
            /// </summary>
            /// <param name="model">The model whose branches is to be returned</param>
            /// <returns>The branch that represents the given model, or null if the model
            /// isn't in the tree.</returns>
            public virtual Branch GetBranch(object model)
            {
                if (model == null)
                    return null;

                Branch br;
                mapObjectToBranch.TryGetValue(model, out br);
                return br;
            }

            /// <summary>
            /// Remove the branch corresponding to the model.  This is a major memory leak 
            /// </summary>
            /// <param name="model"></param>
            private void RemoveBranch(object model)
            {
                Branch br = GetBranch(model);
                if (br != null)
                {
                    mapObjectToBranch.Remove(model);
                    foreach (var cbr in br.Children)
                        RemoveBranch(br);
                }
            }

            /// <summary>
            /// Return the number of visible descendents that are below the given model.
            /// </summary>
            /// <param name="model">The model whose descendent count is to be returned</param>
            /// <returns>The number of visible descendents. 0 if the model doesn't exist or is collapsed</returns>
            public virtual int GetVisibleDescendentCount(object model)
            {
                Branch br = GetBranch(model);
                if (br == null || !br.IsExpanded)
                    return 0;
                else
                    return br.NumberVisibleDescendents;
            }

            /// <summary>
            /// Rebuild the children of the given model, refreshing any cached information held about the given object
            /// </summary>
            /// <param name="model"></param>
            /// <returns>The index of the model in flat list version of the tree</returns>
            public virtual int RebuildChildren(Object model)
            {
                Branch br = GetBranch(model);
                if (br == null || !br.Visible)
                    return -1;

                int count = br.NumberVisibleDescendents;
                br.ClearCachedInfo();

                // Remove the visible descendents from after the branch itself
                int index = GetObjectIndex(model);
                if (count > 0)
                    objectList.RemoveRange(index + 1, count);
                br.FetchChildren();
                if (br.IsExpanded)
                    InsertChildren(br, index + 1);
                return index;
            }

            //------------------------------------------------------------------------------------------
            // Implementation

            /// <summary>
            /// Is the given model expanded?
            /// </summary>
            /// <param name="model"></param>
            /// <returns></returns>
            internal bool IsModelExpanded(object model)
            {
                // Special case: model == null is the container for the roots. This is always expanded
                if (model == null)
                    return true;
                bool isExpanded = false;
                mapObjectToExpanded.TryGetValue(model, out isExpanded);
                return isExpanded;
            }

            /// <summary>
            /// Remember whether or not the given model was expanded
            /// </summary>
            /// <param name="model"></param>
            /// <param name="isExpanded"></param>
            internal void SetModelExpanded(object model, bool isExpanded)
            {
                if (model != null)
                {
                    if (isExpanded)
                        mapObjectToExpanded[model] = true;
                    else
                        mapObjectToExpanded.Remove(model);
                }
            }

            /// <summary>
            /// Insert the children of the given branch into the given position
            /// </summary>
            /// <param name="br">The branch whose children should be inserted</param>
            /// <param name="index">The index where the children should be inserted</param>
            protected virtual void InsertChildren(Branch br, int index)
            {
                // Expand the branch
                br.Expand();
                br.Sort(GetBranchComparer());

                // Insert the branch's visible descendents after the branch itself
                objectList.InsertRange(index, br.Flatten());
                RebuildObjectMap(index);
            }

            /// <summary>
            /// Rebuild our flat internal list of objects.
            /// </summary>
            protected virtual void RebuildList()
            {
                objectList = ArrayList.Adapter(trunk.Flatten());
                List<Branch> filtered = trunk.FilteredChildBranches;
                if (filtered.Count > 0)
                {
                    filtered[0].IsFirstBranch = true;
                    filtered[0].IsOnlyBranch = (filtered.Count == 1);
                }
                RebuildObjectMap(0);
            }

            /// <summary>
            /// Rebuild our reverse index that maps an object to its location
            /// in the filteredObjectList array.
            /// </summary>
            /// <param name="startIndex"></param>
            protected virtual void RebuildObjectMap(int startIndex)
            {
                for (int i = startIndex; i < objectList.Count; i++)
                    mapObjectToIndex[objectList[i]] = i;
            }

            /// <summary>
            /// Create a new branch within this tree
            /// </summary>
            /// <param name="parent"></param>
            /// <param name="model"></param>
            /// <returns></returns>
            internal Branch MakeBranch(Branch parent, object model)
            {
                var br = new Branch(parent, this, model);

                // Remember that the given branch is part of this tree.
                mapObjectToBranch[model] = br;
                return br;
            }

            //------------------------------------------------------------------------------------------

            #region IVirtualListDataSource Members

            /// <summary>
            /// 
            /// </summary>
            /// <param name="n"></param>
            /// <returns></returns>
            public virtual object GetNthObject(int n)
            {
                return objectList[n];
            }

            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public virtual int GetObjectCount()
            {
                return trunk.NumberVisibleDescendents;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="model"></param>
            /// <returns></returns>
            public virtual int GetObjectIndex(object model)
            {
                int index;

                if (model != null && mapObjectToIndex.TryGetValue(model, out index))
                    return index;
                else
                    return -1;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="first"></param>
            /// <param name="last"></param>
            public virtual void PrepareCache(int first, int last)
            {
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="value"></param>
            /// <param name="first"></param>
            /// <param name="last"></param>
            /// <param name="column"></param>
            /// <returns></returns>
            public virtual int SearchText(string value, int first, int last, OLVColumn column)
            {
                return AbstractVirtualListDataSource.DefaultSearchText(value, first, last, column, this);
            }

            /// <summary>
            /// Sort the tree on the given column and in the given order
            /// </summary>
            /// <param name="column"></param>
            /// <param name="order"></param>
            public virtual void Sort(OLVColumn column, SortOrder order)
            {
                lastSortColumn = column;
                lastSortOrder = order;

                // TODO: Need to raise an AboutToSortEvent here

                // Sorting is going to change the order of the branches so clear
                // the "first branch" flag
                foreach (Branch b in trunk.ChildBranches)
                    b.IsFirstBranch = false;

                trunk.Sort(GetBranchComparer());
                RebuildList();
            }

            public virtual void Unsort()
            {
                lastSortColumn = null;
                lastSortOrder = SortOrder.None;
                // Sorting is going to change the order of the branches so clear
                // the "first branch" flag
                foreach (Branch b in trunk.ChildBranches)
                    b.IsFirstBranch = false;
                RebuildList();
            }

            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            protected virtual BranchComparer GetBranchComparer()
            {
                if (lastSortColumn == null)
                    return null;
                else
                    return new BranchComparer(new ModelObjectComparer(lastSortColumn, lastSortOrder,
                                                                      treeView.GetColumn(0), lastSortOrder));
            }

            /// <summary>
            /// Add the given collection of objects to the roots of this tree
            /// </summary>
            /// <param name="modelObjects"></param>
            public virtual void AddObjects(ICollection modelObjects)
            {
                var newRoots = new ArrayList();
                foreach (Object x in treeView.Roots)
                    newRoots.Add(x);
                foreach (Object x in modelObjects)
                    newRoots.Add(x);
                SetObjects(newRoots);
            }

            /// <summary>
            /// Remove all of the given objects from the roots of the tree.
            /// Any objects that is not already in the roots collection is ignored.
            /// </summary>
            /// <param name="modelObjects"></param>
            public virtual void RemoveObjects(ICollection modelObjects)
            {
                var newRoots = new ArrayList();
                foreach (Object x in treeView.Roots)
                    newRoots.Add(x);
                foreach (Object x in modelObjects)
                {
                    newRoots.Remove(x);
                    mapObjectToIndex.Remove(x);
                    RemoveBranch(x);
                }
                SetObjects(newRoots);
            }

            /// <summary>
            /// Set the roots of this tree to be the given collection
            /// </summary>
            /// <param name="collection"></param>
            public virtual void SetObjects(IEnumerable collection)
            {
                // We interpret a SetObjects() call as setting the roots of the tree
                treeView.Roots = collection;
            }

            #endregion

            #region IFilterableDataSource Members

            /// <summary>
            /// 
            /// </summary>
            /// <param name="modelFilter"></param>
            /// <param name="listFilter"></param>
            public void ApplyFilters(IModelFilter modelFilter, IListFilter listFilter)
            {
                this.modelFilter = modelFilter;
                this.listFilter = listFilter;
                RebuildList();
            }

            /// <summary>
            /// Is this list currently being filtered?
            /// </summary>
            internal bool IsFiltering
            {
                get { return treeView.UseFiltering && (modelFilter != null || listFilter != null); }
            }

            /// <summary>
            /// Should the given model be included in this control?
            /// </summary>
            /// <param name="model">The model to consider</param>
            /// <returns>True if it will be included</returns>
            internal bool IncludeModel(object model)
            {
                if (!treeView.UseFiltering)
                    return true;

                if (modelFilter == null)
                    return true;

                return modelFilter.Filter(model);
            }

            #endregion

            //------------------------------------------------------------------------------------------
            // Private instance variables

            private OLVColumn lastSortColumn;
            private SortOrder lastSortOrder;
            private readonly Dictionary<Object, Branch> mapObjectToBranch = new Dictionary<object, Branch>();
            internal Dictionary<Object, bool> mapObjectToExpanded = new Dictionary<object, bool>();
            private readonly Dictionary<Object, int> mapObjectToIndex = new Dictionary<object, int>();
            private ArrayList objectList = new ArrayList();
            private readonly TreeListView treeView;
            private readonly Branch trunk;

            /// <summary>
            /// 
            /// </summary>
            protected IModelFilter modelFilter;

            /// <summary>
            /// 
            /// </summary>
            protected IListFilter listFilter;
        }

        /// <summary>
        /// A Branch represents a sub-tree within a tree
        /// </summary>
        public class Branch
        {
            /// <summary>
            /// Indicators for branches
            /// </summary>
            [Flags]
            public enum BranchFlags
            {
                /// <summary>
                /// FirstBranch of tree
                /// </summary>
                FirstBranch = 1,

                /// <summary>
                /// LastChild of parent
                /// </summary>
                LastChild = 2,

                /// <summary>
                /// OnlyBranch of tree
                /// </summary>
                OnlyBranch = 4
            }

            #region Life and death

            /// <summary>
            /// Create a Branch
            /// </summary>
            /// <param name="parent"></param>
            /// <param name="tree"></param>
            /// <param name="model"></param>
            public Branch(Branch parent, Tree tree, Object model)
            {
                ParentBranch = parent;
                Tree = tree;
                Model = model;
            }

            #endregion

            #region Public properties

            //------------------------------------------------------------------------------------------
            // Properties

            /// <summary>
            /// Get the ancestor branches of this branch, with the 'oldest' ancestor first.
            /// </summary>
            public virtual IList<Branch> Ancestors
            {
                get
                {
                    var ancestors = new List<Branch>();
                    if (ParentBranch != null)
                        ParentBranch.PushAncestors(ancestors);
                    return ancestors;
                }
            }

            private void PushAncestors(IList<Branch> list)
            {
                // This is designed to ignore the trunk (which has no parent)
                if (ParentBranch != null)
                {
                    ParentBranch.PushAncestors(list);
                    list.Add(this);
                }
            }

            /// <summary>
            /// Can this branch be expanded?
            /// </summary>
            public virtual bool CanExpand
            {
                get
                {
                    if (Tree.CanExpandGetter == null || Model == null)
                        return false;
                    else
                        return Tree.CanExpandGetter(Model);
                }
            }

            /// <summary>
            /// Gets or sets our children
            /// </summary>
            public List<Branch> ChildBranches
            {
                get { return childBranches; }
                set { childBranches = value; }
            }

            private List<Branch> childBranches = new List<Branch>();

            /// <summary>
            /// Get/set the model objects that are beneath this branch
            /// </summary>
            public virtual IEnumerable Children
            {
                get
                {
                    var children = new ArrayList();
                    foreach (Branch x in ChildBranches)
                        children.Add(x.Model);
                    return children;
                }
                set
                {
                    ChildBranches.Clear();
                    if (value == null) return;
                    foreach (Object x in value)
                        AddChild(x);
                }
            }

            private void AddChild(object model)
            {
                Branch br = Tree.GetBranch(model);
                if (br == null)
                    br = Tree.MakeBranch(this, model);
                else
                    br.ParentBranch = this;
                ChildBranches.Add(br);
            }

            /// <summary>
            /// Gets a list of all the branches that survive filtering
            /// </summary>
            public List<Branch> FilteredChildBranches
            {
                get
                {
                    if (!Tree.IsFiltering)
                        return ChildBranches;

                    var filtered = new List<Branch>();
                    foreach (Branch b in ChildBranches)
                    {
                        if (Tree.IncludeModel(b.Model))
                            filtered.Add(b);
                        else
                        {
                            // Also include this branch if it has any filtered branches (yes, its recursive)
                            if (b.FilteredChildBranches.Count > 0)
                                filtered.Add(b);
                        }
                    }
                    return filtered;
                }
            }

            /// <summary>
            /// Gets or set whether this branch is expanded
            /// </summary>
            public bool IsExpanded
            {
                get { return Tree.IsModelExpanded(Model); }
                set { Tree.SetModelExpanded(Model, value); }
            }

            /// <summary>
            /// Return true if this branch is the first branch of the entire tree
            /// </summary>
            public virtual bool IsFirstBranch
            {
                get { return ((flags & BranchFlags.FirstBranch) != 0); }
                set
                {
                    if (value)
                        flags |= BranchFlags.FirstBranch;
                    else
                        flags &= ~BranchFlags.FirstBranch;
                }
            }

            /// <summary>
            /// Return true if this branch is the last child of its parent
            /// </summary>
            public virtual bool IsLastChild
            {
                get { return ((flags & BranchFlags.LastChild) != 0); }
                set
                {
                    if (value)
                        flags |= BranchFlags.LastChild;
                    else
                        flags &= ~BranchFlags.LastChild;
                }
            }

            /// <summary>
            /// Return true if this branch is the only top level branch
            /// </summary>
            public virtual bool IsOnlyBranch
            {
                get { return ((flags & BranchFlags.OnlyBranch) != 0); }
                set
                {
                    if (value)
                        flags |= BranchFlags.OnlyBranch;
                    else
                        flags &= ~BranchFlags.OnlyBranch;
                }
            }

            /// <summary>
            /// Gets the depth level of this branch
            /// </summary>
            public int Level
            {
                get
                {
                    if (ParentBranch == null)
                        return 0;
                    else
                        return ParentBranch.Level + 1;
                }
            }

            /// <summary>
            /// Gets or sets which model is represented by this branch
            /// </summary>
            public Object Model { get; set; }

            /// <summary>
            /// Return the number of descendents of this branch that are currently visible
            /// </summary>
            /// <returns></returns>
            public virtual int NumberVisibleDescendents
            {
                get
                {
                    if (!IsExpanded)
                        return 0;

                    List<Branch> filtered = FilteredChildBranches;
                    int count = filtered.Count;
                    foreach (Branch br in filtered)
                        count += br.NumberVisibleDescendents;
                    return count;
                }
            }

            /// <summary>
            /// Gets or sets our parent branch
            /// </summary>
            public Branch ParentBranch { get; set; }

            /// <summary>
            /// Gets or sets our overall tree
            /// </summary>
            public Tree Tree { get; set; }

            /// <summary>
            /// Is this branch currently visible? A branch is visible
            /// if it has no parent (i.e. it's a root), or its parent
            /// is visible and expanded.
            /// </summary>
            public virtual bool Visible
            {
                get
                {
                    if (ParentBranch == null)
                        return true;
                    else
                        return ParentBranch.IsExpanded && ParentBranch.Visible;
                }
            }

            #endregion

            #region Commands

            //------------------------------------------------------------------------------------------
            // Commands

            /// <summary>
            /// Clear any cached information that this branch is holding
            /// </summary>
            public virtual void ClearCachedInfo()
            {
                Children = new ArrayList();
                alreadyHasChildren = false;
            }

            /// <summary>
            /// Collapse this branch
            /// </summary>
            public virtual void Collapse()
            {
                IsExpanded = false;
            }

            /// <summary>
            /// Expand this branch
            /// </summary>
            public virtual void Expand()
            {
                if (CanExpand)
                {
                    IsExpanded = true;
                    FetchChildren();
                }
            }

            /// <summary>
            /// Expand this branch recursively
            /// </summary>
            public virtual void ExpandAll()
            {
                Expand();
                foreach (Branch br in ChildBranches)
                    br.ExpandAll();
            }

            /// <summary>
            /// Fetch the children of this branch.
            /// </summary>
            /// <remarks>This should only be called when CanExpand is true.</remarks>
            public virtual void FetchChildren()
            {
                if (alreadyHasChildren)
                    return;

                alreadyHasChildren = true;

                if (Tree.ChildrenGetter == null)
                    return;

                if (Tree.TreeView.UseWaitCursorWhenExpanding)
                {
                    Cursor previous = Cursor.Current;
                    try
                    {
                        Cursor.Current = Cursors.WaitCursor;
                        Children = Tree.ChildrenGetter(Model);
                    }
                    finally
                    {
                        Cursor.Current = previous;
                    }
                }
                else
                {
                    Children = Tree.ChildrenGetter(Model);
                }
            }

            /// <summary>
            /// Collapse the visible descendents of this branch into list of model objects
            /// </summary>
            /// <returns></returns>
            public virtual IList Flatten()
            {
                var flatList = new ArrayList();
                if (IsExpanded)
                    FlattenOnto(flatList);
                return flatList;
            }

            /// <summary>
            /// Flatten this branch's visible descendents onto the given list.
            /// </summary>
            /// <param name="flatList"></param>
            /// <remarks>The branch itself is <b>not</b> included in the list.</remarks>
            public virtual void FlattenOnto(IList flatList)
            {
                Branch lastBranch = null;
                foreach (Branch br in FilteredChildBranches)
                {
                    lastBranch = br;
                    br.IsLastChild = false;
                    flatList.Add(br.Model);
                    if (br.IsExpanded)
                        br.FlattenOnto(flatList);
                }
                if (lastBranch != null)
                    lastBranch.IsLastChild = true;
            }

            /// <summary>
            /// Force a refresh of all children recursively
            /// </summary>
            public virtual void RefreshChildren()
            {
                if (IsExpanded)
                {
                    FetchChildren();
                    foreach (Branch br in ChildBranches)
                        br.RefreshChildren();
                }
            }

            /// <summary>
            /// Sort the sub-branches and their descendents so they are ordered according
            /// to the given comparer.
            /// </summary>
            /// <param name="comparer">The comparer that orders the branches</param>
            public virtual void Sort(BranchComparer comparer)
            {
                if (ChildBranches.Count == 0)
                    return;

                if (comparer != null)
                    ChildBranches.Sort(comparer);

                foreach (Branch br in ChildBranches)
                    br.Sort(comparer);
            }

            #endregion

            //------------------------------------------------------------------------------------------
            // Private instance variables

            private bool alreadyHasChildren;
            private BranchFlags flags;
        }

        /// <summary>
        /// This class sorts branches according to how their respective model objects are sorted
        /// </summary>
        public class BranchComparer : IComparer<Branch>
        {
            /// <summary>
            /// Create a BranchComparer
            /// </summary>
            /// <param name="actualComparer"></param>
            public BranchComparer(IComparer actualComparer)
            {
                this.actualComparer = actualComparer;
            }

            /// <summary>
            /// Order the two branches
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <returns></returns>
            public int Compare(Branch x, Branch y)
            {
                return actualComparer.Compare(x.Model, y.Model);
            }

            private readonly IComparer actualComparer;
        }

        /// <summary>
        /// Remove any sorting and revert to the given order of the model objects
        /// </summary>
        public override void Unsort()
        {
            ShowGroups = false;
            PrimarySortColumn = null;
            PrimarySortOrder = SortOrder.None;
            TreeModel.Unsort();
            BuildList();
            ShowSortIndicator(LastSortColumn, LastSortOrder);
        }
    }
}