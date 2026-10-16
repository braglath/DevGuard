using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SuperDev.Models;

namespace SuperDev.Views;

public partial class ReorderChecksWindow : Window
{
    private readonly List<ChecklistItem> _items = [];
    private readonly List<Border> _rows = [];
    private Border? _dragRow;
    private Avalonia.Point _dragStart;
    private bool _dragging;

    public ReorderChecksWindow(IReadOnlyList<ChecklistItem> items)
    {
        InitializeComponent();

        foreach (var item in items)
        {
            _items.Add(item);
            var row = CreateRow(item);
            _rows.Add(row);
            OrderList.Children.Add(row);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key != Key.Escape)
            return;

        e.Handled = true;
        Close(null);
    }

    private Border CreateRow(ChecklistItem item)
    {
        var meta = string.IsNullOrWhiteSpace(item.Category)
            ? item.Priority.ToString()
            : $"{item.Category} · {item.Priority}";

        var row = new Border
        {
            Classes = { "reorder-row" },
            Tag = item,
            Child = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Children =
                {
                    new TextBlock
                    {
                        Text = "⋮⋮",
                        Classes = { "reorder-grip" }
                    },
                    new StackPanel
                    {
                        Children =
                        {
                            new TextBlock
                            {
                                Text = item.Title,
                                Classes = { "reorder-title" }
                            },
                            new TextBlock
                            {
                                Text = meta,
                                Classes = { "reorder-meta" }
                            }
                        }
                    }
                }
            }
        };

        row.PointerPressed += Row_PointerPressed;
        row.PointerMoved += Row_PointerMoved;
        row.PointerReleased += Row_PointerReleased;
        return row;
    }

    private void Row_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border row)
            return;

        if (!e.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
            return;

        _dragRow = row;
        _dragStart = e.GetPosition(OrderList);
        _dragging = false;
        e.Pointer.Capture(row);
        e.Handled = true;
    }

    private void Row_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Border row || _dragRow != row)
            return;

        if (!e.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
        {
            FinishDrag(row, e);
            return;
        }

        var position = e.GetPosition(OrderList);
        if (!_dragging)
        {
            var delta = position - _dragStart;
            if (Math.Abs(delta.X) < 4 && Math.Abs(delta.Y) < 4)
                return;

            _dragging = true;
            row.Classes.Add("dragging");
            row.ZIndex = 2;
        }

        ScrollWhileDragging(e.GetPosition(OrderScroll));
        var target = IndexAt(e.GetPosition(OrderList).Y, row);
        MoveRow(row, target);
        e.Handled = true;
    }

    private void Row_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Border row)
            FinishDrag(row, e);
    }

    private void FinishDrag(Border row, PointerEventArgs e)
    {
        if (_dragRow != row)
            return;

        _dragRow = null;
        _dragging = false;
        row.Classes.Remove("dragging");
        row.ZIndex = 0;
        e.Pointer.Capture(null);
    }

    private void ScrollWhileDragging(Avalonia.Point pointer)
    {
        var height = OrderScroll.Bounds.Height;
        if (height <= 0)
            return;

        var offset = OrderScroll.Offset.Y;
        if (pointer.Y < 28)
            offset -= 16;
        else if (pointer.Y > height - 28)
            offset += 16;
        else
            return;

        var max = OrderScroll.Extent.Height - height;
        if (max < 0)
            max = 0;
        if (offset < 0)
            offset = 0;
        if (offset > max)
            offset = max;

        OrderScroll.Offset = new Avalonia.Vector(0, offset);
    }

    private int IndexAt(double y, Border dragging)
    {
        var index = 0;
        foreach (var row in _rows)
        {
            if (ReferenceEquals(row, dragging))
                continue;

            var bounds = row.Bounds;
            if (y > bounds.Y + bounds.Height / 2)
                index++;
        }

        return index;
    }

    private void MoveRow(Border row, int target)
    {
        var from = _rows.IndexOf(row);
        if (from < 0 || target < 0 || target == from || target >= _rows.Count)
            return;

        _rows.RemoveAt(from);
        _items.RemoveAt(from);
        _rows.Insert(target, row);
        _items.Insert(target, (ChecklistItem)row.Tag!);
        OrderList.Children.RemoveAt(from);
        OrderList.Children.Insert(target, row);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) =>
        Close(null);

    private void Done_Click(object? sender, RoutedEventArgs e) =>
        Close(_items.ToArray());
}
