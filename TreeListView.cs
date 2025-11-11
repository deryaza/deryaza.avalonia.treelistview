/*
This program is free software: you can redistribute it and/or modify it under
the terms of the GNU Lesser General Public License as published by the Free
Software Foundation, either version 3 of the License, or (at your option) any
later version. This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License
for more details. You should have received a copy of the GNU Lesser General
Public License along with this program. If not, see <https://www.gnu.org/licenses/>. 
*/

// TODO:
// 1) ShowRowDetails

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;

namespace Deryaza.TreeListView;

public class TreeListView : Grid
{
    public static readonly StyledProperty<object?> SelectedItemExProperty = AvaloniaProperty.Register<TreeListView, object?>(nameof(SelectedItemEx));
    public static readonly StyledProperty<GridView?> ViewProperty = AvaloniaProperty.Register<TreeListView, GridView?>(nameof(View));
    public static readonly StyledProperty<bool> ShowRowDetailsProperty = AvaloniaProperty.Register<TreeListView, bool>(nameof(ShowRowDetails));
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<TreeListView, IEnumerable?>(nameof(ItemsSource));
    public static readonly StyledProperty<string?> ChildrenPropertyNameProperty = AvaloniaProperty.Register<TreeListView, string?>(nameof(ChildrenPropertyName));

    public string? ChildrenPropertyName { get => GetValue(ChildrenPropertyNameProperty); set => SetValue(ChildrenPropertyNameProperty, value); }

    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    public bool ShowRowDetails { get => GetValue(ShowRowDetailsProperty); set => SetValue(ShowRowDetailsProperty, value); }

    public object? SelectedItemEx { get => GetValue(SelectedItemExProperty); set => SetValue(SelectedItemExProperty, value); }

    public GridView? View { get => GetValue(ViewProperty); set => SetValue(ViewProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ViewProperty)
        {
            UpdateGridView(change.GetNewValue<GridView>());
        }
        else if (change.Property == ItemsSourceProperty)
        {
            UpdateItemsSource(change.GetOldValue<IEnumerable>(), change.GetNewValue<IEnumerable>());
        }
        else if (change.Property == ChildrenPropertyNameProperty)
        {
            UpdateSubPath(change.GetNewValue<string>());
        }
    }

    readonly StackPanel _rows;
    readonly Row _header;
    public TreeListView()
    {
        RowDefinitions.Add(new(GridLength.Auto));
        RowDefinitions.Add(new(GridLength.Star));

        _header = new Row() { [Grid.RowProperty] = 0 };
        _rows = new StackPanel();

        Children.Add(_header);
        Children.Add(new ScrollViewer() { Content = _rows, [Grid.RowProperty] = 1 });
    }

    private void UpdateItemsSource(IEnumerable oldEnumerable, IEnumerable newEnumerable)
    {
        INotifyCollectionChanged? ntc = oldEnumerable as INotifyCollectionChanged;
        if (ntc != null)
        {
            ntc.CollectionChanged -= OnCollectionChanged;
        }

        ntc = newEnumerable as INotifyCollectionChanged;
        if (ntc != null)
        {
            ntc.CollectionChanged += OnCollectionChanged;
        }

        OnCollectionChanged(null, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

        if (newEnumerable == null)
        {
            return;
        }

        OnCollectionChanged(newEnumerable,
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add,
                    newEnumerable as IList));
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Reset:
                {
                    _rows.Children.Clear();
                    break;
                }
            case NotifyCollectionChangedAction.Add:
                {
                    if (e.NewItems == null)
                    {
                        return;
                    }

                    foreach (object item in e.NewItems)
                    {
                        var row = new TreeListViewRow(0)
                        {
                            DataContext = item,
                        };
                        row.UpdateGridView(_header.Children);
                        row.UpdateSubPath(ChildrenPropertyName, item);
                        _rows.Children.Add(row);
                    }

                    break;
                }
            case NotifyCollectionChangedAction.Remove:
                {
                    if (e.OldItems == null)
                    {
                        return;
                    }

                    _rows.Children.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                    break;
                }
        }
    }

    class TreeListViewRootRow : Row
    {
        public static StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<TreeListViewRow, bool>(nameof(IsSelected), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
        public bool IsSelected

        {
            get => GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            IsSelected = true;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IsPointerOverProperty || change.Property == IsSelectedProperty)
            {
                UpdateBackground();
            }
        }

        private void UpdateBackground()
        {
            IBrush? brush;
            if (IsPointerOver || IsSelected)
            {
                brush = this.TryFindResource("TreeListViewSelectedBrush", ActualThemeVariant, out var obj)
                        ? obj as IBrush
                        : this.TryFindResource("SystemControlHighlightListAccentLowBrush", ActualThemeVariant, out obj)
                            ? obj as IBrush
                            : Brushes.Gray;
            }
            else
            {
                brush = null;
            }

            SetValue(BackgroundProperty, brush, BindingPriority.Style);
        }
    }

    class TreeListViewRow : StackPanel
    {
        public static StyledProperty<bool> IsExpandedProperty = AvaloniaProperty.Register<TreeListViewRow, bool>(nameof(IsExpanded), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
        public bool IsExpanded
        {
            get => GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        public static StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<TreeListViewRow, bool>(nameof(IsSelected), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
        public bool IsSelected

        {
            get => GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public static StyledProperty<bool> HasChildrenProperty = AvaloniaProperty.Register<TreeListViewRow, bool>(nameof(HasChildren), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
        public bool HasChildren
        {
            get => GetValue(HasChildrenProperty);
            set => SetValue(HasChildrenProperty, value);
        }

        private IList<Control>? _currentColumns;

        string? _childPropertyName;

        readonly StackPanel _subRows;
        readonly TreeListViewRootRow _rootRow;

        public TreeListViewRow(int level)
        {
            Children.Add(_rootRow = new());
            Children.Add(_subRows = new());
            _subRows.IsVisible = false;

            _rootRow[!TreeListViewRootRow.IsSelectedProperty] = this[!!IsSelectedProperty];

            Level = level;
        }

        public int Level { get; }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ParentProperty && change.NewValue == null)
            {
                _rootRow.Children.Clear();
                _subRows.Children.Clear();
            }
            else if (change.Property == DataContextProperty)
            {
                UpdateSubPath(_childPropertyName, change.NewValue);
            }
            else if (change.Property == IsExpandedProperty)
            {
                var isExpanded = change.GetNewValue<bool>();
                if (isExpanded)
                {
                    _subRows.IsVisible = true;
                }
                else
                {
                    _subRows.IsVisible = false;
                    foreach (TreeListViewRow r in _subRows.Children.Cast<TreeListViewRow>()) r.IsExpanded = false;
                }
            }
            else if (change.Property == IsSelectedProperty)
            {
                this.FindAncestorOfType<TreeListView>().OnRowIsSelectedChanged(this, IsSelected);
            }
        }

        internal void UpdateSubPath(string? name, object? dataContext)
        {
            _childPropertyName = name;

            _subRows.Children.Clear();

            if (name == null || dataContext == null)
            {
                return;
            }

            if (dataContext is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged -= ItemPropertyChanged;
                npc.PropertyChanged += ItemPropertyChanged;
            }

            ItemPropertyChanged(dataContext, new PropertyChangedEventArgs(name));
        }

        object? _currentCollection;

        private void ItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender == null || _childPropertyName == null) return;
            var prop = sender.GetType().GetProperty(_childPropertyName);
            if (prop == null) return;

            var newCollection = prop.GetValue(sender);

            if (_currentCollection is INotifyCollectionChanged currentNotify)
            {
                currentNotify.CollectionChanged -= ItemCollectionChanged;
            }

            ItemCollectionChanged(null, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

            _currentCollection = newCollection;
            if (newCollection is not IList initialItems)
            {
                return;
            }

            if (initialItems is INotifyCollectionChanged newNotify)
            {
                newNotify.CollectionChanged += ItemCollectionChanged;
            }

            ItemCollectionChanged(initialItems, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, initialItems));
        }

        private void ItemCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    {
                        _subRows.Children.Clear();
                        break;
                    }
                case NotifyCollectionChangedAction.Add:
                    {
                        if (e.NewItems == null)
                        {
                            break;
                        }

                        foreach (object item in e.NewItems)
                        {
                            var row = new TreeListViewRow(Level + 1)
                            {
                                DataContext = item,
                            };

                            if (_currentColumns != null) row.UpdateGridView(_currentColumns);
                            row.UpdateSubPath(_childPropertyName, item);
                            _subRows.Children.Add(row);
                        }

                        break;
                    }
                case NotifyCollectionChangedAction.Remove:
                    {
                        if (e.OldItems == null)
                        {
                            break;
                        }

                        _subRows.Children.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                        break;
                    }
            }

            HasChildren = _subRows.Children.Count > 0;
        }

        internal void UpdateGridView(IList<Control> columns)
        {
            if (_currentColumns == columns)
            {
                return;
            }

            _currentColumns = columns;
            _rootRow.Children.Clear();

            for (int i = 0; i < columns.Count; i++)
            {
                HeaderCell headerCell = (HeaderCell)columns[i];
                GridViewColumn col = headerCell.Column;
                var cell = new TreeListViewCell(headerCell, Level)
                {
                    Content = col?.CellTemplate?.Build(DataContext),
                    [!TreeListViewCell.IsExpandedProperty] = this[!!IsExpandedProperty],
                    [!TreeListViewCell.IsExpandableProperty] = this[!HasChildrenProperty],
                };
                cell.SetIndex(i);

                _rootRow.Children.Add(cell);
            }

            foreach (TreeListViewRow r in _subRows.Children.Cast<TreeListViewRow>()) r.UpdateGridView(columns);
        }

        internal void Swap(int oldIndex, int newIndex)
        {
            _rootRow.Swap(oldIndex, newIndex);
            foreach (TreeListViewRow r in _subRows.Children.Cast<TreeListViewRow>()) r.Swap(oldIndex, newIndex);
        }

        internal IEnumerable<TreeListViewRow> EnumerateChildren() => _subRows.Children.Cast<TreeListViewRow>();
    }

    class TreeListViewCell(HeaderCell headerColumn, int level) : Cell
    {
        public static StyledProperty<bool> IsExpandedProperty = AvaloniaProperty.Register<TreeListViewCell, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);
        public bool IsExpanded
        {
            get => GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        public static StyledProperty<bool> IsExpandableProperty = AvaloniaProperty.Register<TreeListViewRow, bool>(nameof(IsExpandable));
        public bool IsExpandable
        {
            get => GetValue(IsExpandableProperty);
            set => SetValue(IsExpandableProperty, value);
        }

        static readonly Geometry OpenGeometry = Geometry.Parse("M 0 0 L 8 0 L 4 4 Z");
        static readonly Geometry ClosedGeometry = Geometry.Parse("M 0 0 L 4 4 L 0 8 Z");

        private Path? _path;
        bool _isFirst;

        public override void SetIndex(int i)
        {
            Padding = i == 0 ? new(5 + level * 15, 0, 0, 0) : default;

            if (!IsExpandable)
            {
                _isFirst = i == 0;
                if (_path != null)
                {
                    _path = null;
                    var sp = (StackPanel)Content;
                    var content = sp.Children[1];
                    sp.Children.Clear();
                    Content = content;
                }

                return;
            }

            if (_path != null && i != 0)
            {
                _isFirst = false;
                _path = null;
                var sp = (StackPanel)Content;
                var content = sp.Children[1];
                sp.Children.Clear();
                Content = content;
            }
            else if (_path == null && i == 0)
            {
                _isFirst = true;

                _path = new Path()
                {
                    Data = IsExpanded ? OpenGeometry : ClosedGeometry,
                    Fill = Brushes.DarkGray,
                    Width = 7,
                    Height = 7,
                    Stretch = Stretch.Fill,
                    Margin = new(5, 3, 3, 2)
                };

                var content = Content;
                Content = null;
                Content = new StackPanel()
                {
                    Orientation = Orientation.Horizontal,
                    Children = { _path, content as Control }
                };
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (!_isFirst || !IsExpandable) return;
            IsExpanded = !IsExpanded;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ParentProperty)
            {
                if (change.NewValue == null)
                {
                    headerColumn.Cells.Remove(this);
                }
                else
                {
                    headerColumn.Cells.Add(this);
                }
            }
            else if (change.Property == IsExpandedProperty && _isFirst && IsExpandable)
            {
                _path.Data = change.GetNewValue<bool>() ? OpenGeometry : ClosedGeometry;
            }
            else if (change.Property == IsExpandableProperty)
            {
                SetIndex(_isFirst && change.GetNewValue<bool>() ? 0 : 1);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);
            var headerWidth = headerColumn.DesiredSize.Width;
            if (!double.IsNaN(headerColumn.Width))
            {
                return size.WithWidth(headerWidth);
            }
            if (headerWidth >= size.Width)
            {
                return size.WithWidth(headerWidth);
            }

            headerColumn.ChildColumnWidth = size.Width;
            return size;
        }

    }

    class Row : Control
    {
        public static readonly StyledProperty<IBrush?> BackgroundProperty =
                        AvaloniaProperty.Register<Border, IBrush?>(nameof(Background));

        /// <summary>
        /// Gets or sets a brush with which to paint the background.
        /// </summary>
        public IBrush? Background
        {
            get => GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        public Controls Children { get; } = [];

        static Row()
        {
            AffectsRender<Row>(BackgroundProperty);
        }

        public Row()
        {
            Children.CollectionChanged += ChildrenChanged;
        }

        private void ChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    LogicalChildren.InsertRange(e.NewStartingIndex, e.NewItems!.Cast<Control>());
                    VisualChildren.InsertRange(e.NewStartingIndex, e.NewItems!.Cast<Visual>());
                    break;

                case NotifyCollectionChangedAction.Move:
                    LogicalChildren.MoveRange(e.OldStartingIndex, e.OldItems!.Count, e.NewStartingIndex);
                    VisualChildren.MoveRange(e.OldStartingIndex, e.OldItems!.Count, e.NewStartingIndex);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    LogicalChildren.RemoveAll(e.OldItems!.OfType<Control>().ToList());
                    VisualChildren.RemoveAll(e.OldItems!.OfType<Visual>());
                    break;

                case NotifyCollectionChangedAction.Replace:
                    for (var i = 0; i < e.OldItems!.Count; ++i)
                    {
                        var index = i + e.OldStartingIndex;
                        var child = (Control)e.NewItems![i]!;
                        LogicalChildren[index] = child;
                        VisualChildren[index] = child;
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    throw new NotSupportedException();
            }

            InvalidateMeasure();
        }

        public void Swap(int oldIndex, int newIndex)
        {
            var a = (Cell)Children[oldIndex];
            var b = (Cell)Children[newIndex];

            Children.RemoveAt(oldIndex);
            Children.Insert(newIndex, a);

            Children.RemoveAt(Children.IndexOf(b));
            Children.Insert(oldIndex, b);

            b.SetIndex(oldIndex);
            a.SetIndex(newIndex);
        }

        public sealed override void Render(DrawingContext context)
        {
            base.Render(context);

            var bounds = Bounds;
            var renderSize = bounds.Size;

            var background = Background;
            if (background != null)
            {
                context.FillRectangle(background, bounds);
            }

            context.FillRectangle(Brushes.Transparent, new(renderSize));
            context.FillRectangle(Brushes.Black, new(0, renderSize.Height, renderSize.Width, 1));
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var constrainedSize = availableSize.WithWidth(double.PositiveInfinity);
            var desiredSize = new Size();
            foreach (var child in Children)
            {
                child.Measure(constrainedSize);
                var childSize = child.DesiredSize;
                desiredSize = new(desiredSize.Width + childSize.Width, Math.Max(desiredSize.Height, childSize.Height));
            }
            return desiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double prevChild = 0;
            double height = finalSize.Height;

            foreach (var child in Children)
            {
                height = Math.Max(height, child.DesiredSize.Height);
                child.Arrange(new Rect(prevChild, 0, child.DesiredSize.Width, height));
                prevChild += child.DesiredSize.Width;
            }

            return finalSize;
        }
    }

    abstract class Cell : ContentControl
    {
        public virtual void SetIndex(int i) { }
        public sealed override void Render(DrawingContext context)
        {
            base.Render(context);
            var renderSize = Bounds.Size;
            context.FillRectangle(Brushes.Black, new(renderSize.Width - 1, 0, 1, renderSize.Height));
        }
    }

    class HeaderCell(GridViewColumn column) : Cell
    {
        private double childColumnWidth;

        internal double ChildColumnWidth
        {
            get => childColumnWidth;
            set
            {
                childColumnWidth = value;
                InvalidateMeasure();
            }
        }

        internal readonly List<TreeListViewCell> Cells = [];

        public GridViewColumn Column { get; } = column;

        protected override Size MeasureCore(Size availableSize)
        {
            Size size = base.MeasureCore(availableSize);

            if (double.IsNaN(Width) && ChildColumnWidth > size.Width)
            {
                size = size.WithWidth(ChildColumnWidth);
            }

            foreach (var c in Cells) c.InvalidateMeasure();

            return size;
        }
    }

    void UpdateGridView(GridView cols)
    {
        _header.Children.Clear();

        for (int i = 0; i < cols.Columns.Count; i++)
        {
            GridViewColumn col = cols.Columns[i];
            _header.Children.Add(
                    new HeaderCell(col)
                    {
                        MinWidth = 25,
                        [!HeaderCell.ContentProperty] = col[!GridViewColumn.HeaderProperty],
                        [!HeaderCell.WidthProperty] = col[!GridViewColumn.WidthProperty]
                    });
        }

        foreach (TreeListViewRow tlv in _rows.Children.Cast<TreeListViewRow>()) tlv.UpdateGridView(_header.Children);

        InvalidateMeasure();
    }

    private void UpdateSubPath(string childrenPropName)
    {
        foreach (TreeListViewRow tlv in _rows.Children.Cast<TreeListViewRow>()) tlv.UpdateSubPath(childrenPropName, tlv.DataContext);
    }

    private IEnumerable<TreeListViewRow> EnumerateFlatten(IEnumerable<TreeListViewRow> rows)
    {
        foreach (var r in rows)
        {
            yield return r;
            foreach (var cr in EnumerateFlatten(r.EnumerateChildren()))
            {
                yield return cr;
            }
        }
    }
    private void OnRowIsSelectedChanged(TreeListViewRow treeListViewRow, bool isSelected)
    {
        if (isSelected)
        {
            foreach (TreeListViewRow tlv in EnumerateFlatten(_rows.Children.Cast<TreeListViewRow>()))
            {
                tlv.IsSelected = treeListViewRow == tlv;
            }

            SelectedItemEx = treeListViewRow.DataContext;
        }
        else if (treeListViewRow.DataContext == SelectedItemEx)
        {
            SelectedItemEx = null;
        }
    }

    #region Columns resize and reorder
    const int TLV_HeaderResizeMargin = 5;

    private readonly static Cursor _resizeCursor = new(StandardCursorType.SizeWestEast);
    private Cursor? _oldCursor;
    private HeaderCell? _resizingCell;

    private Control? _draggableControl;

    private HeaderCell? GetHeaderCell(Point pos)
    {
        HeaderCell? hitChild = null;
        foreach (var child in _header.Children)
        {
            if (child.Bounds.Contains(pos))
            {
                hitChild = child as HeaderCell;
                break;
            }
        }
        return hitChild;
    }

    private HeaderCell? GetResizeCell(PointerEventArgs e)
    {
        if (GetHeaderCell(e.GetPosition(_header)) is not { } hitChild)
        {
            return null;
        }

        var pos = e.GetPosition(hitChild);

        if (pos.X < TLV_HeaderResizeMargin)
        {
            var idx = _header.Children.IndexOf(hitChild);
            return idx > 0 ? (HeaderCell)_header.Children[idx - 1] : null;
        }
        else if (hitChild.Bounds.Width - pos.X < TLV_HeaderResizeMargin)
        {
            return hitChild;
        }
        else
        {
            return null;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (_draggableControl != null)
        {
            StopDrag();
        }

        if (Cursor == _resizeCursor)
        {
            if (GetResizeCell(e) is { } cell)
            {
                _resizingCell = cell;
            }
            else
            {
                _resizingCell = null;
            }

            return;
        }

        var hitChild = GetHeaderCell(e.GetPosition(_header));
        if (hitChild == null)
        {
            return;
        }

        object? content = hitChild.Content;
        if (content is Control)
        {
            _draggableControl = new Rectangle()
            {
                Width = _header.DesiredSize.Width,
                Height = _header.DesiredSize.Height,
                Fill = new VisualBrush
                {
                    Visual = hitChild,
                    Stretch = Stretch.None,
                    AlignmentX = AlignmentX.Left,
                }
            };
        }
        else
        {
            _draggableControl = new ContentControl() { Content = content };
        }

        _draggableControl[Canvas.LeftProperty] = hitChild.Bounds.Left;
        _draggableControl.IsEnabled = false;
        _draggableControl.IsHitTestVisible = false;
        _draggableControl.Opacity = 0.7;
        _draggableControl.Tag = hitChild;

        AdornerLayer.SetAdorner(_header, new Canvas() { Children = { _draggableControl } });
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_draggableControl != null)
        {
            _draggableControl[Canvas.LeftProperty] = e.GetPosition(_header).X;
            return;
        }

        if (_resizingCell != null)
        {
            var width = _resizingCell.Bounds.Width - e.GetPosition(_resizingCell).X;
            var res = _resizingCell.Bounds.Width - width;
            if (res > 0 && res > _resizingCell.MinWidth)
            {
                _resizingCell.Width = res;
            }
        }
        else if (GetResizeCell(e) is not null)
        {
            if (Cursor != _resizeCursor)
            {
                _oldCursor = Cursor;
                Cursor = _resizeCursor;
            }
        }
        else if (Cursor == _resizeCursor)
        {
            Cursor = _oldCursor;
        }
    }

    private void StopDrag()
    {
        AdornerLayer.SetAdorner(_header, null);
        _draggableControl = null;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_resizingCell != null)
        {
            _resizingCell = null;
            Cursor = _oldCursor;
            return;
        }

        if (_draggableControl == null)
        {
            return;
        }

        HeaderCell? dragCell = _draggableControl.Tag as HeaderCell;
        StopDrag();
        if (dragCell == null)
        {
            return;
        }

        var cell = GetHeaderCell(e.GetPosition(_header).WithY(0));
        if (cell == null || dragCell == cell)
        {
            return;
        }

        int oldIndex = _header.Children.IndexOf(dragCell);
        int newIndex = _header.Children.IndexOf(cell);
        _header.Swap(oldIndex, newIndex);
        foreach (TreeListViewRow row in _rows.Children.Cast<TreeListViewRow>())
        {
            row.Swap(oldIndex, newIndex);
        }
    }
    #endregion
}

public class GridView : AvaloniaObject
{
    private readonly ObservableCollection<GridViewColumn> _columns = [];
    [Content]
    public ObservableCollection<GridViewColumn> Columns => _columns;
}

public class GridViewColumn : AvaloniaObject
{
    public static readonly StyledProperty<object?> HeaderProperty = AvaloniaProperty.Register<GridViewColumn, object?>(nameof(Header));
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    public static readonly StyledProperty<double> WidthProperty = AvaloniaProperty.Register<GridViewColumn, double>(nameof(Width), defaultValue: double.NaN);
    public double Width { get => GetValue(WidthProperty); set => SetValue(WidthProperty, value); }

    public static readonly StyledProperty<IDataTemplate?> CellTemplateProperty = AvaloniaProperty.Register<GridViewColumn, IDataTemplate?>(nameof(CellTemplate));
    [InheritDataTypeFromItems(nameof(TreeListView.ItemsSource), AncestorType = typeof(TreeListView))]
    public IDataTemplate? CellTemplate { get => GetValue(CellTemplateProperty); set => SetValue(CellTemplateProperty, value); }

}
