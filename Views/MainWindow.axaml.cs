using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DevGuard.Models;
using DevGuard.Services;
using System.Threading.Tasks;
namespace DevGuard.Views;

public partial class MainWindow : Window
{
    private readonly ChecklistTemplateService _checklistService;

    public MainWindow()
    {
        InitializeComponent();

        _checklistService = new ChecklistTemplateService();
    }

    private WorkType? _selectedWorkType;

    private void WorkflowButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.Tag is not string typeName)
            return;

        if (!Enum.TryParse<WorkType>(
                typeName,
                out var workType))
            return;

        _selectedWorkType = workType;

        ClearTemplateSelection();

        button.Classes.Add("selected");
    }


    private void ClearTemplateSelection()
    {
        BugButton.Classes.Remove("selected");
        FeatureButton.Classes.Remove("selected");
        DevelopmentButton.Classes.Remove("selected");
        TestingButton.Classes.Remove("selected");
        PullRequestButton.Classes.Remove("selected");
    }

    private async void Create_Click(
    object? sender,
    RoutedEventArgs e)
    {
        var workTitle = WorkTitleTextBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(workTitle))
        {
            await ShowMessage(
                "Please enter a title for your work.");

            return;
        }

        if (_selectedWorkType == null)
        {
            await ShowMessage(
                "Please select a template.");

            return;
        }

        var checklist = _checklistService
            .GetTemplate(_selectedWorkType.Value);

        var checklistWindow = new ChecklistWindow(
            _selectedWorkType.Value,
            workTitle,
            checklist);

        checklistWindow.Show();

        Close();
    }

    private async Task ShowMessage(string message)
    {
        var dialog = new StyledDialog(
            "DevGuard",
            message);

        await dialog.ShowDialog(this);
    }
}