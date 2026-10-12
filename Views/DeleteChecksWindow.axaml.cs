using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace SuperDev.Views;

public partial class DeleteChecksWindow : Window
{
    private readonly List<CheckBox> _checks = [];

    public DeleteChecksWindow(IReadOnlyList<string> checks)
    {
        InitializeComponent();

        for (var index = 0; index < checks.Count; index++)
        {
            var box = new CheckBox
            {
                Content = new TextBlock
                {
                    Text = checks[index],
                    Classes = { "check-label" },
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center
                },
                VerticalAlignment = VerticalAlignment.Center
            };
            box.IsCheckedChanged += (_, _) => UpdateActions();
            _checks.Add(box);
            ChecksPanel.Children.Add(box);
        }

        EmptyChecksText.IsVisible = checks.Count == 0;
        UpdateActions();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || e.Key != Key.Escape)
            return;

        e.Handled = true;
        Close(null);
    }

    private void SelectAll_Click(object? sender, RoutedEventArgs e) =>
        SetAll(true);

    private void Clear_Click(object? sender, RoutedEventArgs e) =>
        SetAll(false);

    private void SetAll(bool selected)
    {
        foreach (var box in _checks)
            box.IsChecked = selected;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) =>
        Close(null);

    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        var selected = SelectedIndexes();
        if (selected.Count == 0)
            return;

        Close(selected);
    }

    private List<int> SelectedIndexes() =>
        _checks
            .Select((box, index) => (box, index))
            .Where(entry => entry.box.IsChecked == true)
            .Select(entry => entry.index)
            .ToList();

    private void UpdateActions()
    {
        var count = _checks.Count(box => box.IsChecked == true);
        SelectionCount.Text = count == 1 ? "1 selected" : $"{count} selected";
        DeleteButton.IsEnabled = count > 0;
    }
}
