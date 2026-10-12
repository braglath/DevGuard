
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SuperDev.Models;
using SuperDev.Services;

namespace SuperDev.Views;

public partial class ChecklistView : UserControl
{
   private enum StatusFilter
   {
      All,
      Open,
      Completed
   }

   private enum PriorityFilter
   {
      All,
      Critical,
      Required,
      Conditional,
      Review
   }

   private enum ChecklistSort
   {
      Order,
      Priority,
      Category,
      Title
   }

   private readonly WorkType _workType;
   private readonly ObservableCollection<ChecklistItem> _checklist;
   private readonly ObservableCollection<ChecklistItem> _visible = new();
   private bool _viewReady;
   private bool _detailsOpen;
   private TopLevel? _inputRoot;
   public string WorkTitle { get; private set; }

   public string BranchName { get; private set; }

   public string PullRequestUrl { get; private set; }

   public WorkType WorkType => _workType;

   public IReadOnlyList<ChecklistItem> Items => _checklist;

   public event EventHandler? DocumentEdited;

   public event EventHandler? DocumentCompleted;

   public event EventHandler? SaveRequested;

   public event EventHandler? SaveAsRequested;

   public bool HasIncompleteRequiredChecks() =>
       _checklist.Any(item =>
           !item.IsCompleted &&
           (item.Priority == ChecklistPriority.Critical ||
            item.Priority == ChecklistPriority.Required));

   public ChecklistView(
       WorkType workType,
       string workTitle,
       string branchName,
       string pullRequestUrl,
       List<ChecklistItem> checklist)
   {
      InitializeComponent();

      _workType = workType;
      WorkTitle = workTitle;
      BranchName = branchName;
      PullRequestUrl = pullRequestUrl;
      _checklist = new ObservableCollection<ChecklistItem>(checklist);

      WorkflowTitleBox.Text = workTitle;
      BranchNameBox.Text = branchName;
      PullRequestBox.Text = pullRequestUrl;
      _detailsOpen = !string.IsNullOrWhiteSpace(branchName) ||
                     !string.IsNullOrWhiteSpace(pullRequestUrl);
      ApplyDetailsVisibility();
      WorkflowDescription.Text = GetWorkflowDescription(workType);
      ChecklistItems.ItemsSource = _visible;

      StatusFilterBox.SelectedIndex = 0;
      PriorityFilterBox.SelectedIndex = 0;
      SortBox.SelectedIndex = 0;
      _viewReady = true;

      ApplyView();
      UpdateProgress();
   }

   private static string GetWorkflowDescription(WorkType workType)
   {
      return workType switch
      {
         WorkType.Bug =>
             "[Bug] Validate, fix, test and verify the issue before release.",
         WorkType.Feature =>
             "[Feature] Validate requirements, implement, test and verify the new feature.",
         WorkType.Development =>
             "[Development] Follow development, testing and code review standards.",
         WorkType.Testing =>
             "[Testing] Validate functionality, regression scenarios and edge cases.",
         WorkType.PullRequest =>
             "[Pull Request] Review, approve, merge and verify the pull request.",
         WorkType.Blank =>
             "Create a custom checklist for your work.",
         _ => string.Empty
      };
   }

   private void UpdateProgress()
   {
      var total = _checklist.Count;
      var completed = _checklist.Count(x => x.IsCompleted);

      ProgressText.Text = $"{completed} / {total} completed";
      ProgressBar.Value = total == 0
          ? 0
          : (double)completed / total * 100;
   }

   private Window? GetOwnerWindow() =>
       TopLevel.GetTopLevel(this) as Window;

   private void CheckBox_Click(object? sender, RoutedEventArgs e)
   {
      if (sender is not CheckBox checkBox ||
          checkBox.DataContext is not ChecklistItem item)
         return;

      item.IsCompleted = checkBox.IsChecked == true;

      MarkEdited();
   }

   private void Save_Click(object? sender, RoutedEventArgs e) =>
       SaveRequested?.Invoke(this, EventArgs.Empty);

   private void SaveAs_Click(object? sender, RoutedEventArgs e) =>
       SaveAsRequested?.Invoke(this, EventArgs.Empty);

   private void MarkEdited()
   {
      UpdateProgress();
      ApplyView();
      DocumentEdited?.Invoke(this, EventArgs.Empty);
   }

   protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
   {
      base.OnAttachedToVisualTree(e);
      _inputRoot = TopLevel.GetTopLevel(this);
      _inputRoot?.AddHandler(
          PointerPressedEvent,
          Title_DismissPointerPressed,
          RoutingStrategies.Tunnel,
          handledEventsToo: true);
   }

   protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
   {
      _inputRoot?.RemoveHandler(PointerPressedEvent, Title_DismissPointerPressed);
      _inputRoot = null;
      base.OnDetachedFromVisualTree(e);
   }

   private void Title_DismissPointerPressed(object? sender, PointerPressedEventArgs e)
   {
      var focused = FocusedDetailBox();
      if (focused is null)
         return;

      if (e.Source is Visual source && IsInside(source, focused))
         return;

      _inputRoot?.FocusManager?.Focus(null, NavigationMethod.Pointer, KeyModifiers.None);
   }

   private static bool IsInside(Visual source, Visual target)
   {
      var current = source;
      while (current is not null)
      {
         if (current == target)
            return true;

         current = current.GetVisualParent();
      }

      return false;
   }

   private TextBox? FocusedDetailBox()
   {
      if (WorkflowTitleBox.IsFocused)
         return WorkflowTitleBox;

      if (BranchNameBox.IsFocused)
         return BranchNameBox;

      if (PullRequestBox.IsFocused)
         return PullRequestBox;

      return null;
   }

   public void CommitDetails()
   {
      CommitTitle();
      CommitBranch();
      CommitPullRequest();
   }

   private void Title_LostFocus(object? sender, RoutedEventArgs e) =>
       CommitTitle();

   private void AddDetails_Click(object? sender, RoutedEventArgs e)
   {
      _detailsOpen = true;
      DetailsPanel.IsVisible = true;
      AddDetailsButton.IsVisible = false;
      BranchNameBox.Focus();
   }

   private void Branch_LostFocus(object? sender, RoutedEventArgs e)
   {
      CommitBranch();
      ScheduleDetailsRefresh();
   }

   private void PullRequest_LostFocus(object? sender, RoutedEventArgs e)
   {
      CommitPullRequest();
      ScheduleDetailsRefresh();
   }

   private void ScheduleDetailsRefresh() =>
       Dispatcher.UIThread.Post(ApplyDetailsVisibility);

   private void ApplyDetailsVisibility()
   {
      var hasDetails =
          !string.IsNullOrWhiteSpace(BranchName) ||
          !string.IsNullOrWhiteSpace(PullRequestUrl);
      var editing = BranchNameBox.IsFocused || PullRequestBox.IsFocused;

      if (hasDetails || editing)
         _detailsOpen = true;
      else
         _detailsOpen = false;

      DetailsPanel.IsVisible = _detailsOpen;
      AddDetailsButton.IsVisible = !_detailsOpen;
   }

   private void PullRequest_TextChanged(object? sender, TextChangedEventArgs e) =>
       OpenPullRequestButton.IsEnabled = ExternalLink.CanOpen(PullRequestBox.Text);

   private async void OpenPullRequest_Click(object? sender, RoutedEventArgs e)
   {
      CommitPullRequest();

      if (ExternalLink.TryOpen(PullRequestUrl))
         return;

      var owner = GetOwnerWindow();
      if (owner is null)
         return;

      var dialog = new StyledDialog("Super Dev", "That PR link could not be opened.");
      await dialog.ShowDialog(owner);
   }

   private void CommitTitle()
   {
      var title = WorkflowTitleBox.Text?.Trim() ?? string.Empty;
      if (title.Length == 0)
      {
         WorkflowTitleBox.Text = WorkTitle;
         return;
      }

      if (title == WorkTitle)
      {
         if (WorkflowTitleBox.Text != WorkTitle)
            WorkflowTitleBox.Text = WorkTitle;
         return;
      }

      WorkTitle = title;
      WorkflowTitleBox.Text = title;
      DocumentEdited?.Invoke(this, EventArgs.Empty);
   }

   private void CommitBranch()
   {
      var branch = BranchNameBox.Text?.Trim() ?? string.Empty;
      if (BranchNameBox.Text != branch)
         BranchNameBox.Text = branch;

      if (branch == BranchName)
         return;

      BranchName = branch;
      DocumentEdited?.Invoke(this, EventArgs.Empty);
   }

   private void CommitPullRequest()
   {
      var url = PullRequestBox.Text?.Trim() ?? string.Empty;
      if (PullRequestBox.Text != url)
         PullRequestBox.Text = url;

      OpenPullRequestButton.IsEnabled = ExternalLink.CanOpen(url);

      if (url == PullRequestUrl)
         return;

      PullRequestUrl = url;
      DocumentEdited?.Invoke(this, EventArgs.Empty);
   }

   private void Search_TextChanged(object? sender, TextChangedEventArgs e) =>
       ApplyView();

   private void View_Changed(object? sender, SelectionChangedEventArgs e) =>
       ApplyView();

   private void ResetView_Click(object? sender, RoutedEventArgs e)
   {
      SearchTextBox.Text = string.Empty;
      StatusFilterBox.SelectedIndex = 0;
      PriorityFilterBox.SelectedIndex = 0;
      SortBox.SelectedIndex = 0;
      ApplyView();
   }

   private void ApplyView()
   {
      if (!_viewReady)
         return;

      var matches = _checklist.Select((item, index) => (item, index));

      var search = SearchTextBox.Text?.Trim();
      if (!string.IsNullOrEmpty(search))
      {
         matches = matches.Where(entry =>
             entry.item.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             entry.item.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             entry.item.Notes.Contains(search, StringComparison.OrdinalIgnoreCase));
      }

      matches = (StatusFilter)StatusFilterBox.SelectedIndex switch
      {
         StatusFilter.Open => matches.Where(entry => !entry.item.IsCompleted),
         StatusFilter.Completed => matches.Where(entry => entry.item.IsCompleted),
         _ => matches
      };

      matches = (PriorityFilter)PriorityFilterBox.SelectedIndex switch
      {
         PriorityFilter.Critical => matches.Where(entry => entry.item.Priority == ChecklistPriority.Critical),
         PriorityFilter.Required => matches.Where(entry => entry.item.Priority == ChecklistPriority.Required),
         PriorityFilter.Conditional => matches.Where(entry => entry.item.Priority == ChecklistPriority.Conditional),
         PriorityFilter.Review => matches.Where(entry => entry.item.Priority == ChecklistPriority.Review),
         _ => matches
      };

      matches = (ChecklistSort)SortBox.SelectedIndex switch
      {
         ChecklistSort.Priority => matches
             .OrderBy(entry => entry.item.Priority)
             .ThenBy(entry => entry.index),
         ChecklistSort.Category => matches
             .OrderBy(entry => entry.item.Category, StringComparer.OrdinalIgnoreCase)
             .ThenBy(entry => entry.index),
         ChecklistSort.Title => matches
             .OrderBy(entry => entry.item.Title, StringComparer.OrdinalIgnoreCase)
             .ThenBy(entry => entry.index),
         _ => matches
      };

      var visible = matches.Select(entry => entry.item).ToList();
      if (visible.Count == _visible.Count && visible.SequenceEqual(_visible))
      {
         UpdateViewCount(visible.Count);
         return;
      }

      _visible.Clear();
      foreach (var item in visible)
         _visible.Add(item);

      UpdateViewCount(visible.Count);
   }

   private void UpdateViewCount(int shown)
   {
      var total = _checklist.Count;
      var narrowed = HasSearchOrFilter();
      ViewCountText.Text = narrowed ? $"{shown} of {total}" : string.Empty;
      ViewCountText.IsVisible = narrowed;
      EmptyFilterText.IsVisible = total > 0 && shown == 0;

      if (SortBox.SelectedIndex <= 0 && shown > 1)
         ChecklistItems.Classes.Add("reorder");
      else
         ChecklistItems.Classes.Remove("reorder");
   }

   private bool HasSearchOrFilter() =>
       !string.IsNullOrWhiteSpace(SearchTextBox.Text) ||
       StatusFilterBox.SelectedIndex > 0 ||
       PriorityFilterBox.SelectedIndex > 0;

   private void MoveUp_Click(object? sender, RoutedEventArgs e) =>
       MoveCheck(sender, -1);

   private void MoveDown_Click(object? sender, RoutedEventArgs e) =>
       MoveCheck(sender, 1);

   private void MoveCheck(object? sender, int direction)
   {
      if (SortBox.SelectedIndex > 0)
         return;

      if (sender is not Button button || button.Tag is not ChecklistItem item)
         return;

      var visibleIndex = _visible.IndexOf(item);
      var neighborIndex = visibleIndex + direction;
      if (visibleIndex < 0 || neighborIndex < 0 || neighborIndex >= _visible.Count)
         return;

      var from = _checklist.IndexOf(item);
      var to = _checklist.IndexOf(_visible[neighborIndex]);
      if (from < 0 || to < 0 || from == to)
         return;

      _checklist.Move(from, to);
      MarkEdited();
   }

   private void RefreshVisibleItem(ChecklistItem item)
   {
      var index = _visible.IndexOf(item);
      if (index < 0)
         return;

      _visible.RemoveAt(index);
      _visible.Insert(index, item);
   }

   private async void AddCheck_Click(object? sender, RoutedEventArgs e)
   {
      var owner = GetOwnerWindow();
      if (owner is null)
         return;

      var dialog = new AddCheckWindow();
      var result = await dialog.ShowDialog<ChecklistItem?>(owner);

      if (result is null)
         return;

      _checklist.Add(result);
      MarkEdited();
   }

   private async void ViewNotes_Click(object? sender, RoutedEventArgs e)
   {
      if (sender is not Button button ||
          button.Tag is not ChecklistItem item ||
          !item.HasNotes)
         return;

      var owner = GetOwnerWindow();
      if (owner is null)
         return;

      var dialog = new NotesWindow(item.Title, item.Detail, item.Notes);
      await dialog.ShowDialog(owner);
   }

   private async void EditCheck_Click(object? sender, RoutedEventArgs e)
   {
      if (sender is not Button button ||
          button.Tag is not ChecklistItem item)
         return;

      var owner = GetOwnerWindow();
      if (owner is null)
         return;

      var dialog = new AddCheckWindow(item);
      var result = await dialog.ShowDialog<ChecklistItem?>(owner);

      if (result is null)
         return;

      item.Title = result.Title;
      item.Category = result.Category;
      item.Priority = result.Priority;
      item.Notes = result.Notes;
      item.IsCompleted = result.IsCompleted;

      MarkEdited();
      RefreshVisibleItem(item);
   }

   private async void RemoveCheck_Click(object? sender, RoutedEventArgs e)
   {
      if (sender is not Button button ||
          button.Tag is not ChecklistItem item)
         return;

      var owner = GetOwnerWindow();
      if (owner is null)
         return;

      var dialog = new StyledDialog(
          "Remove checklist item?",
          $"Are you sure you want to remove \"{item.Title}\"?",
          "Remove",
          "Cancel",
          danger: true);

      var result = await dialog.ShowDialog<bool>(owner);

      if (!result)
         return;

      _checklist.Remove(item);
      MarkEdited();
   }

   private async void Complete_Click(object? sender, RoutedEventArgs e)
   {
      var owner = GetOwnerWindow();
      if (owner is null)
         return;

      var incompleteRequired = _checklist.Count(item =>
          !item.IsCompleted &&
          (item.Priority == ChecklistPriority.Critical ||
           item.Priority == ChecklistPriority.Required));

      if (incompleteRequired > 0)
      {
         var dialog = new StyledDialog(
             "Checklist incomplete",
             $"You still have {incompleteRequired} " +
             "Critical/Required check(s) incomplete.");

         await dialog.ShowDialog(owner);
         return;
      }

      // The document flag is stored by the window. It is not written until Save.
      DocumentCompleted?.Invoke(this, EventArgs.Empty);

      var successDialog = new StyledDialog(
          "Checklist complete",
          "All required checks have been completed. Save the checklist to keep this.");

      await successDialog.ShowDialog(owner);
   }
}
