using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SuperDev.Views;

public sealed class LinkChecklistChoice
{
    public LinkChecklistChoice(int? openIndex = null, bool chooseFile = false)
    {
        OpenIndex = openIndex;
        ChooseFile = chooseFile;
    }

    public int? OpenIndex { get; }

    public bool ChooseFile { get; }
}

public partial class LinkChecklistWindow : Window
{
    public LinkChecklistWindow(IReadOnlyList<string> openChecklists)
    {
        InitializeComponent();

        if (openChecklists.Count == 0)
        {
            EmptyText.IsVisible = true;
            return;
        }

        for (var index = 0; index < openChecklists.Count; index++)
        {
            var choice = index;
            var button = new Button
            {
                Content = openChecklists[index],
                Classes = { "choice" }
            };
            button.Click += (_, _) => Close(new LinkChecklistChoice(choice));
            OpenList.Children.Add(button);
        }
    }

    private void ChooseFile_Click(object? sender, RoutedEventArgs e) =>
        Close(new LinkChecklistChoice(chooseFile: true));

    private void Cancel_Click(object? sender, RoutedEventArgs e) =>
        Close(null);
}
