using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DevGuard.Models;
using DevGuard.Services;

namespace DevGuard.Views;

public partial class MainWindow : Window
{
    private readonly ChecklistTemplateService _checklistService = new();
    private readonly ChecklistFileService _fileService = new();
    private readonly List<ChecklistTab> _tabs = new();

    private WorkType? _selectedWorkType;
    private ChecklistTab? _activeTab;
    private bool _fileOperationInProgress;

    public MainWindow()
    {
        InitializeComponent();
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
        var now = DateTime.UtcNow;
        var document = new ChecklistDocument
        {
            FormatVersion = ChecklistDocument.CurrentFormatVersion,
            Id = Guid.NewGuid(),
            Title = workTitle,
            WorkType = workType,
            CreatedOn = now,
            UpdatedOn = now,
            IsCompleted = false,
            Items = _checklistService.GetTemplate(workType)
        };

        OpenDocumentTab(document, filePath: null, isDirty: true);

        WorkTitleTextBox.Text = string.Empty;
        _selectedWorkType = null;
        ClearTemplateSelection();
    }

    private void OpenDocumentTab(
        ChecklistDocument document,
        string? filePath,
        bool isDirty)
    {
        var view = new ChecklistView(
            document.WorkType,
            document.Title,
            document.Items);

        // Title sizes the tab. It ellipsizes at the width left inside the tab max
        // (190) after horizontal padding (16), the gap before the close button (8),
        // the close button (24), and the completion mark when it is showing.
        var titleText = new TextBlock
        {
            Text = document.Title,
            MaxWidth = 118,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        var completionMark = new TextBlock
        {
            Text = "✓",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#4ADE80")),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            IsVisible = false
        };

        ToolTip.SetTip(completionMark, "Completed");

        var titleRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children = { completionMark, titleText }
        };

        var closeButton = new Button
        {
            Content = "×"
        };

        closeButton.Classes.Add("tab-close");
        ToolTip.SetTip(closeButton, "Close tab");

        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto"),
            ColumnSpacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        Grid.SetColumn(titleRow, 0);
        Grid.SetColumn(closeButton, 1);

        headerGrid.Children.Add(titleRow);
        headerGrid.Children.Add(closeButton);

        var tabHeader = new Border
        {
            Classes = { "workspace-tab" },
            Height = 40,
            Padding = new Avalonia.Thickness(8, 3),
            Child = headerGrid
        };

        var tab = new ChecklistTab(document, view, tabHeader, titleText, completionMark)
        {
            FilePath = filePath,
            IsDirty = isDirty
        };

        view.DocumentEdited += (_, _) => OnDocumentEdited(tab);
        view.DocumentCompleted += (_, _) => OnDocumentCompleted(tab);
        view.SaveRequested += (_, _) => _ = RunFileOperation(() => SaveAsync(tab));
        view.SaveAsRequested += (_, _) => _ = RunFileOperation(() => SaveAsAsync(tab));

        tabHeader.PointerPressed += (_, e) =>
        {
            if (e.Source is Button)
                return;

            ActivateTab(tab);
        };

        closeButton.Click += (_, _) => _ = RunFileOperation(() => CloseTabAsync(tab));

        _tabs.Add(tab);
        TabsPanel.Children.Add(tabHeader);

        UpdateTabChrome(tab);
        ActivateTab(tab);
    }

    private async Task CloseTabAsync(ChecklistTab tab)
    {
        if (_tabs.IndexOf(tab) < 0)
            return;

        if (tab.IsDirty)
        {
            var choice = await PromptUnsavedAsync(tab.Document.Title);
            if (choice == ClosePromptResult.Cancel)
                return;

            if (choice == ClosePromptResult.Save && !await SaveAsync(tab))
                return;
        }

        RemoveTab(tab);
    }

    private void RemoveTab(ChecklistTab tab)
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || e.Key != Key.S)
            return;

        var hasCommand = e.KeyModifiers.HasFlag(KeyModifiers.Control)
            || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        if (!hasCommand || ActiveTab() is not { } tab)
            return;

        e.Handled = true;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            _ = RunFileOperation(() => SaveAsAsync(tab));
        else
            _ = RunFileOperation(() => SaveAsync(tab));
    }

    private void Open_Click(object? sender, RoutedEventArgs e) =>
        _ = RunFileOperation(OpenAsync);

    private void OpenMenu_Click(object? sender, EventArgs e) =>
        _ = RunFileOperation(OpenAsync);

    private void Save_Click(object? sender, RoutedEventArgs e) =>
        SaveActiveTab(saveAs: false);

    private void SaveMenu_Click(object? sender, EventArgs e) =>
        SaveActiveTab(saveAs: false);

    private void SaveAs_Click(object? sender, RoutedEventArgs e) =>
        SaveActiveTab(saveAs: true);

    private void SaveAsMenu_Click(object? sender, EventArgs e) =>
        SaveActiveTab(saveAs: true);

    private void SaveActiveTab(bool saveAs)
    {
        if (ActiveTab() is not { } tab)
            return;

        _ = RunFileOperation(() => saveAs ? SaveAsAsync(tab) : SaveAsync(tab));
    }

    private ChecklistTab? ActiveTab() =>
        HomePanel.IsVisible ? null : _activeTab;

    private async Task RunFileOperation(Func<Task> action)
    {
        if (_fileOperationInProgress)
            return;

        _fileOperationInProgress = true;
        try
        {
            await action();
        }
        catch (ChecklistFileException ex)
        {
            await ShowMessage(ex.Message);
        }
        finally
        {
            _fileOperationInProgress = false;
        }
    }

    private async Task OpenAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open checklist",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("DevGuard checklist")
                {
                    Patterns = ["*.devguard.json", "*.json"],
                    MimeTypes = ["application/json"]
                }
            ]
        });

        if (files.Count == 0)
            return;

        var path = LocalPath(files[0]);
        if (path is null)
        {
            await ShowMessage("That location can't be used. Choose a file on this computer.");
            return;
        }

        path = Path.GetFullPath(path);

        var alreadyOpen = FindTabByPath(path);
        if (alreadyOpen is not null)
        {
            ActivateTab(alreadyOpen);
            return;
        }

        var document = await _fileService.LoadAsync(path);
        OpenDocumentTab(document, path, isDirty: false);
    }

    private async Task<bool> SaveAsync(ChecklistTab tab)
    {
        if (string.IsNullOrWhiteSpace(tab.FilePath))
            return await SaveAsAsync(tab);

        return await WriteDocumentAsync(tab, tab.FilePath);
    }

    private async Task<bool> SaveAsAsync(ChecklistTab tab)
    {
        var path = await PickSavePathAsync(tab);
        if (path is null)
            return false;

        return await WriteDocumentAsync(tab, path);
    }

    private async Task<string?> PickSavePathAsync(ChecklistTab tab)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save checklist",
            SuggestedFileName = SuggestedFileName(tab.Document.Title),
            DefaultExtension = "devguard.json",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType("DevGuard checklist")
                {
                    Patterns = ["*.devguard.json"],
                    MimeTypes = ["application/json"]
                }
            ]
        });

        if (file is null)
            return null;

        var path = LocalPath(file);
        if (path is null)
        {
            await ShowMessage("That location can't be used. Choose a file on this computer.");
            return null;
        }

        return EnsureChecklistExtension(Path.GetFullPath(path));
    }

    private async Task<bool> WriteDocumentAsync(ChecklistTab tab, string path)
    {
        var other = FindTabByPath(path);
        if (other is not null && other != tab)
        {
            await ShowMessage(
                $"\"{other.Document.Title}\" is already open from that file. Choose a different file.");
            return false;
        }

        var replaceDifferentDocument = false;
        if (File.Exists(path))
        {
            try
            {
                var existing = await _fileService.LoadAsync(path);
                if (existing.Id != tab.Document.Id)
                {
                    var overwrite = await ConfirmAsync(
                        "Overwrite a different checklist?",
                        $"That file is \"{existing.Title}\", a different checklist. Overwriting it replaces that document.",
                        "Overwrite",
                        "Cancel",
                        danger: true);

                    if (!overwrite)
                        return false;

                    replaceDifferentDocument = true;
                }
            }
            catch (ChecklistFileException)
            {
                var replace = await ConfirmAsync(
                    "Replace this file?",
                    "That file isn't a DevGuard checklist. Replace it with this checklist?",
                    "Replace",
                    "Cancel",
                    danger: true);

                if (!replace)
                    return false;

                replaceDifferentDocument = true;
            }
        }

        CopyViewOntoDocument(tab);

        // UpdatedOn advances only after the write succeeds. CreatedOn stays put.
        var previousUpdatedOn = tab.Document.UpdatedOn;
        tab.Document.UpdatedOn = DateTime.UtcNow;

        try
        {
            await _fileService.SaveAsync(path, tab.Document, replaceDifferentDocument);
        }
        catch (Exception ex) when (ex is ChecklistFileException or IOException or UnauthorizedAccessException)
        {
            tab.Document.UpdatedOn = previousUpdatedOn;
            await ShowMessage(ex is ChecklistFileException fileEx
                ? fileEx.Message
                : "The checklist could not be saved.");
            return false;
        }

        tab.FilePath = path;
        tab.IsDirty = false;
        UpdateTabChrome(tab);
        await ShowMessage($"Saved \"{tab.Document.Title}\".");
        return true;
    }

    private static void CopyViewOntoDocument(ChecklistTab tab)
    {
        tab.Document.Title = tab.View.WorkTitle;
        tab.Document.WorkType = tab.View.WorkType;
        tab.Document.Items = tab.View.Items.ToList();
    }

    private void OnDocumentEdited(ChecklistTab tab)
    {
        tab.IsDirty = true;

        // Opening never reaches this. Completion stays until a Critical or Required
        // check is incomplete, so the flag still means those checks passed.
        if (tab.Document.IsCompleted && tab.View.HasIncompleteRequiredChecks())
            tab.Document.IsCompleted = false;

        UpdateTabChrome(tab);
    }

    private void OnDocumentCompleted(ChecklistTab tab)
    {
        if (tab.Document.IsCompleted)
            return;

        tab.Document.IsCompleted = true;
        tab.IsDirty = true;
        UpdateTabChrome(tab);
    }

    private void UpdateTabChrome(ChecklistTab tab)
    {
        tab.CompletionMark.IsVisible = tab.Document.IsCompleted;

        var location = tab.IsDirty
            ? "Unsaved changes"
            : tab.FilePath ?? "Not saved yet";

        var tip = tab.Document.IsCompleted
            ? $"{tab.Document.Title} — Completed — {location}"
            : $"{tab.Document.Title} — {location}";

        ToolTip.SetTip(tab.HeaderContainer, tip);
        ToolTip.SetTip(tab.TitleText, tip);
    }

    private ChecklistTab? FindTabByPath(string path)
    {
        var fullPath = NormalizePath(path);
        if (fullPath is null)
            return null;

        return _tabs.FirstOrDefault(tab =>
            string.Equals(NormalizePath(tab.FilePath), fullPath, StringComparison.OrdinalIgnoreCase));
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static string? LocalPath(IStorageFile file)
    {
        var path = file.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            return path;

        if (file.Path.IsAbsoluteUri && file.Path.IsFile)
            return file.Path.LocalPath;

        return null;
    }

    private static string SuggestedFileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(title.Trim()
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray()).Trim();

        if (cleaned.Length == 0)
            cleaned = "Checklist";

        return cleaned + ".devguard.json";
    }

    private static string EnsureChecklistExtension(string path)
    {
        const string extension = ".devguard.json";

        while (path.EndsWith(extension + extension, StringComparison.OrdinalIgnoreCase))
            path = path[..^extension.Length];

        if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            return path;

        if (path.EndsWith(extension + ".json", StringComparison.OrdinalIgnoreCase))
            return path[..^".json".Length];

        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return string.Concat(path.AsSpan(0, path.Length - ".json".Length), extension);

        return path + extension;
    }

    private async Task<ClosePromptResult> PromptUnsavedAsync(string title)
    {
        var dialog = new StyledDialog(
            "Save changes?",
            $"\"{title}\" has unsaved changes.",
            "Save",
            "Cancel",
            "Discard");

        return await dialog.ShowDialog<ClosePromptResult>(this);
    }

    private async Task<bool> ConfirmAsync(
        string title,
        string message,
        string primary,
        string secondary,
        bool danger = false)
    {
        var dialog = new StyledDialog(title, message, primary, secondary, danger);
        return await dialog.ShowDialog<bool>(this);
    }

    private async Task ShowMessage(string message)
    {
        var dialog = new StyledDialog("DevGuard", message);
        await dialog.ShowDialog(this);
    }

    private sealed class ChecklistTab(
        ChecklistDocument document,
        ChecklistView view,
        Border headerContainer,
        TextBlock titleText,
        TextBlock completionMark)
    {
        public ChecklistDocument Document { get; } = document;
        public ChecklistView View { get; } = view;
        public Border HeaderContainer { get; } = headerContainer;
        public TextBlock TitleText { get; } = titleText;
        public TextBlock CompletionMark { get; } = completionMark;
        public string? FilePath { get; set; }
        public bool IsDirty { get; set; }
    }
}
