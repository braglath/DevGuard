using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SuperDev.Views;

public partial class NotesWindow : Window
{
    public NotesWindow()
    {
        InitializeComponent();
    }

    public NotesWindow(string title, string detail, string notes)
        : this()
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Notes" : title;
        TitleText.Text = title;
        DetailText.Text = detail;
        NotesText.Source = notes;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.Enter or Key.Return)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void Close_Click(object? sender, RoutedEventArgs e) =>
        Close();
}
