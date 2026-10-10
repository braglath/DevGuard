
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DevGuard.Models;
using DevGuard.Services;

namespace DevGuard.Views;

public partial class MainWindow : Window
{
    private readonly ChecklistTemplateService _checklistService;
    private readonly List<ChecklistTab> _tabs = new();

    private WorkType? _selectedWorkType;
    private ChecklistTab? _activeTab;

    public MainWindow()
    {
        InitializeComponent();
        _checklistService = new ChecklistTemplateService();
    }

    private void WorkflowButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not string typeName ||
            !Enum.TryParse<WorkType>(typeName, out var workType))
        {
            return;
        }

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
        BlankButton.Classes.Remove("selected");
    }



    private async void Create_Click(
        object? sender,
        RoutedEventArgs e)
    {
        var workTitle = WorkTitleTextBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(workTitle))
        {
            await ShowMessage("Please enter a title for your work.");
            return;
        }

        if (_selectedWorkType is null)
        {
            await ShowMessage("Please select a template.");
            return;
        }

        var workType = _selectedWorkType.Value;
        var checklist = _checklistService.GetTemplate(workType);

        var view = new ChecklistView(workType, workTitle, checklist);

        // Title sizes the tab. It ellipsizes at the width left inside the tab max
        // (190) after horizontal padding (16), the gap before the close button (8),
        // and the close button (24).
        var titleText = new TextBlock
        {
            Text = workTitle,
            MaxWidth = 142,
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        ToolTip.SetTip(titleText, workTitle);

        // Close button.
        var closeButton = new Button
        {
            Content = "×"
        };

        closeButton.Classes.Add("tab-close");
        ToolTip.SetTip(closeButton, "Close tab");

        // Title and close button inside one tab.
        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto"),
            ColumnSpacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        Grid.SetColumn(titleText, 0);
        Grid.SetColumn(closeButton, 1);

        headerGrid.Children.Add(titleText);
        headerGrid.Children.Add(closeButton);

        var tabHeader = new Border
        {
            Classes = { "workspace-tab" },
            Height = 40,
            Padding = new Avalonia.Thickness(8, 3),
            Child = headerGrid
        };

        var tab = new ChecklistTab(workTitle, view, tabHeader);

        // Clicking the tab activates it; clicking × closes it.
        tabHeader.PointerPressed += (_, e) =>
       {
           if (e.Source is Button)
               return;

           ActivateTab(tab);
       };

        closeButton.Click += (_, _) => CloseTab(tab);

        _tabs.Add(tab);
        TabsPanel.Children.Add(tabHeader);

        ActivateTab(tab);

        WorkTitleTextBox.Text = string.Empty;
        _selectedWorkType = null;
        ClearTemplateSelection();
    }



    private void CloseTab(ChecklistTab tab)
    {
        var index = _tabs.IndexOf(tab);

        if (index < 0)
            return;

        TabsPanel.Children.Remove(tab.HeaderContainer);
        _tabs.Remove(tab);

        if (_activeTab != tab)
        {
            TabBar.IsVisible = _tabs.Count > 0;
            return;
        }

        if (_tabs.Count > 0)
        {
            var nextIndex = Math.Min(index, _tabs.Count - 1);
            ActivateTab(_tabs[nextIndex]);
        }
        else
        {
            _activeTab = null;
            WorkspaceContent.Content = null;
            WorkspaceContent.IsVisible = false;
            TabBar.IsVisible = false;
            HomePanel.IsVisible = true;
        }
    }

    private void ActivateTab(ChecklistTab tab)
    {
        _activeTab = tab;

        foreach (var openTab in _tabs)
            openTab.HeaderContainer.Classes.Remove("selected");

        tab.HeaderContainer.Classes.Add("selected");

        WorkspaceContent.Content = tab.View;
        WorkspaceContent.IsVisible = true;
        TabBar.IsVisible = true;
        HomePanel.IsVisible = false;
    }

    private void NewTab_Click(
        object? sender,
        RoutedEventArgs e)
    {
        ShowHomeScreen();
    }

    private void ShowHomeScreen()
    {
        WorkspaceContent.IsVisible = false;
        WorkspaceContent.Content = null;

        HomePanel.IsVisible = true;
        TabBar.IsVisible = _tabs.Count > 0;
    }

    private async Task ShowMessage(string message)
    {
        var dialog = new StyledDialog("DevGuard", message);
        await dialog.ShowDialog(this);
    }

    private sealed class ChecklistTab(
        string title,
        ChecklistView view,
        Border headerContainer)
    {
        public string Title { get; } = title;
        public ChecklistView View { get; } = view;
        public Border HeaderContainer { get; } = headerContainer;
    }
}
