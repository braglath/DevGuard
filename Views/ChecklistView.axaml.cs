
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
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
   private bool _filtersVisible = true;
   private bool _headerDetailsVisible = true;
   private bool _deadlineOpening;
   private TopLevel? _inputRoot;
   public string WorkTitle { get; private set; }

   public string BranchName { get; private set; }

   public string RepositoryUrl { get; private set; }

   public string PullRequestUrl { get; private set; }

   public DateOnly? Deadline { get; private set; }

   public WorkType WorkType => _workType;

   public IReadOnlyList<ChecklistItem> Items => _checklist;

   public event EventHandler? DocumentEdited;

   public event EventHandler? DocumentCompleted;

   public event EventHandler? SaveRequested;

   public event EventHandler? SaveAsRequested;

   public event EventHandler? CopyRequested;

   public bool HasIncompleteRequiredChecks() =>
       _checklist.Any(item =>
           !item.IsCompleted &&
           (item.Priority == ChecklistPriority.Critical ||
            item.Priority == ChecklistPriority.Required));

   public ChecklistView(
       WorkType workType,
       string workTitle,
       string branchName,
       string repositoryUrl,
       string pullRequestUrl,
       DateOnly? deadline,
       List<ChecklistItem> checklist)
   {
      InitializeComponent();

      _workType = workType;
      WorkTitle = workTitle;
      BranchName = branchName;
      RepositoryUrl = repositoryUrl;
      PullRequestUrl = pullRequestUrl;
      Deadline = deadline;
      _checklist = new ObservableCollection<ChecklistItem>(checklist);

      WorkflowTitleBox.Text = workTitle;
      BranchNameBox.Text = branchName;
      RepositoryBox.Text = repositoryUrl;
      PullRequestBox.Text = pullRequestUrl;
      OpenRepositoryButton.IsEnabled = ExternalLink.CanOpen(repositoryUrl);
      _detailsOpen = !string.IsNullOrWhiteSpace(branchName) ||
                     !string.IsNullOrWhiteSpace(repositoryUrl) ||
                     !string.IsNullOrWhiteSpace(pullRequestUrl);
      ApplyDetailsVisibility();
      UpdateDeadlinePresentation();
      ChecklistItems.ItemsSource = _visible;

      StatusFilterBox.SelectedIndex = 0;
      PriorityFilterBox.SelectedIndex = 0;
      SortBox.SelectedIndex = 0;
      _viewReady = true;

      ApplyView();
      UpdateProgress();
   }

   private static string FormatTemplateLine(WorkType workType, string? reminder)
   {
      var template = workType switch
      {
         WorkType.Bug => "Bug Fix",
         WorkType.Feature => "Feature",
         WorkType.Development => "Development",
         WorkType.Testing => "Testing",
         WorkType.PullRequest => "Pull Request",
         WorkType.Blank => "Blank Checklist",
         WorkType.Daily => "Daily",
         _ => string.Empty
      };

      if (string.IsNullOrWhiteSpace(reminder))
         return template;

      return string.IsNullOrWhiteSpace(template)
          ? reminder.Trim()
          : $"{template} · {reminder.Trim()}";
   }

   private void UpdateDeadlinePresentation()
   {
      WorkflowDescription.Text = FormatTemplateLine(_workType, reminder: null);
      ApplyDeadlineLink(DeadlineButton, Deadline);
   }

   private void DeadlineFlyout_Opened(object? sender, EventArgs e) =>
       ShowDeadlineCalendar(DeadlineCalendar, Deadline);

   private void DeadlineCalendar_Changed(object? sender, SelectionChangedEventArgs e)
   {
      if (_deadlineOpening || DeadlineCalendar.SelectedDate is not { } selected)
         return;

      var next = DeadlineValue.FromCalendar(selected);
      DeadlineButton.Flyout?.Hide();

      if (Deadline == next)
         return;

      Deadline = next;
      UpdateDeadlinePresentation();
      DocumentEdited?.Invoke(this, EventArgs.Empty);
   }

   private void ClearDeadline_Click(object? sender, RoutedEventArgs e)
   {
      if (Deadline is null)
         return;

      Deadline = null;
      UpdateDeadlinePresentation();
      DocumentEdited?.Invoke(this, EventArgs.Empty);
   }

   private void ItemDeadlineLink_Loaded(object? sender, RoutedEventArgs e)
   {
      if (sender is Button button && button.DataContext is ChecklistItem item)
         ApplyDeadlineLink(button, item.Deadline);
   }

   private void ItemDeadlineFlyout_Opened(object? sender, EventArgs e)
   {
      if (sender is not Flyout flyout || flyout.Content is not Calendar calendar)
         return;

      var deadline = flyout.Target?.DataContext is ChecklistItem item
          ? item.Deadline
          : null;
      ShowDeadlineCalendar(calendar, deadline);
   }

   private void ItemDeadlineCalendar_Changed(object? sender, SelectionChangedEventArgs e)
   {
      if (_deadlineOpening || sender is not Calendar calendar || calendar.SelectedDate is not { } selected)
         return;

      if (calendar.FindAncestorOfType<Popup>()?.PlacementTarget is not Button button ||
          button.DataContext is not ChecklistItem item)
         return;

      var next = DeadlineValue.FromCalendar(selected);
      button.Flyout?.Hide();

      if (item.Deadline == next)
         return;

      item.Deadline = next;
      ApplyDeadlineLink(button, next);
      MarkEdited();
   }

   private void ClearItemDeadline_Click(object? sender, RoutedEventArgs e)
   {
      if (sender is not Button clear || clear.DataContext is not ChecklistItem item || item.Deadline is null)
         return;

      if (clear.Parent is not Panel panel)
         return;

      var link = panel.Children.OfType<Button>().FirstOrDefault(button => button != clear);
      if (link is null)
         return;

      item.Deadline = null;
      ApplyDeadlineLink(link, null);
      MarkEdited();
   }

   private void ShowDeadlineCalendar(Calendar calendar, DateOnly? deadline)
   {
      _deadlineOpening = true;
      var selected = deadline is { } date ? DeadlineValue.ToCalendar(date) : (DateTime?)null;
      calendar.SelectedDate = selected;
      calendar.DisplayDate = selected ?? DateTime.Today;
      _deadlineOpening = false;
   }

   private static void ApplyDeadlineLink(Button link, DateOnly? deadline)
   {
      link.Content = deadline is { } date
          ? $"Deadline: {DeadlineValue.Format(date)}"
          : "Pick deadline";

      if (link.Parent is not Panel panel)
         return;

      var clear = panel.Children.OfType<Button>().FirstOrDefault(button => button != link);
      if (clear is not null)
         clear.IsVisible = deadline is not null;
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

      if (item.IsCompleted)
         SuperDev.SuccessSound.PlayCheck();

      MarkEdited();
   }

   private void Save_Click(object? sender, RoutedEventArgs e) =>
       SaveRequested?.Invoke(this, EventArgs.Empty);

   private void SaveAs_Click(object? sender, RoutedEventArgs e) =>
       SaveAsRequested?.Invoke(this, EventArgs.Empty);

   private void Copy_Click(object? sender, RoutedEventArgs e) =>
       CopyRequested?.Invoke(this, EventArgs.Empty);

   public int RemoveItems(IReadOnlyList<ChecklistItem> items)
   {
      var removed = 0;
      foreach (var item in items)
      {
         if (_checklist.Remove(item))
            removed++;
      }

      if (removed > 0)
         MarkEdited();

      return removed;
   }

   public int AppendCopies(IReadOnlyList<ChecklistItem> items)
   {
      var added = 0;
      foreach (var item in items)
      {
         _checklist.Insert(added, CopyItem(item));
         added++;
      }

      if (added > 0)
         MarkEdited();

      return added;
   }

   private static ChecklistItem CopyItem(ChecklistItem item) =>
       new()
       {
          Id = Guid.NewGuid(),
          Title = item.Title,
          Category = item.Category,
          Priority = item.Priority,
          Notes = item.Notes,
          Deadline = item.Deadline,
          IsCompleted = item.IsCompleted
       };

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

      if (RepositoryBox.IsFocused)
         return RepositoryBox;

      if (PullRequestBox.IsFocused)
         return PullRequestBox;

      return null;
   }

   public void CommitDetails()
   {
      CommitTitle();
      CommitBranch();
      CommitRepository();
      CommitPullRequest();
   }

   private void Title_LostFocus(object? sender, RoutedEventArgs e) =>
       CommitTitle();

   private void AddDetails_Click(object? sender, RoutedEventArgs e)
   {
      _detailsOpen = true;
      DetailsPanel.IsVisible = true;
      AddDetailsButton.IsVisible = false;
      RepositoryBox.Focus();
   }

   private void Branch_LostFocus(object? sender, RoutedEventArgs e)
   {
      CommitBranch();
      ScheduleDetailsRefresh();
   }

   private void Repository_LostFocus(object? sender, RoutedEventArgs e)
   {
      CommitRepository();
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
          !string.IsNullOrWhiteSpace(RepositoryUrl) ||
          !string.IsNullOrWhiteSpace(PullRequestUrl);
      var editing = BranchNameBox.IsFocused ||
                    RepositoryBox.IsFocused ||
                    PullRequestBox.IsFocused;

      if (hasDetails || editing)
         _detailsOpen = true;
      else
         _detailsOpen = false;

      DetailsPanel.IsVisible = _detailsOpen;
      AddDetailsButton.IsVisible = !_detailsOpen;
   }

   private void Repository_TextChanged(object? sender, TextChangedEventArgs e) =>
       OpenRepositoryButton.IsEnabled = ExternalLink.CanOpen(RepositoryBox.Text);

   private async void OpenRepository_Click(object? sender, RoutedEventArgs e)
   {
      CommitRepository();

      if (ExternalLink.TryOpen(RepositoryUrl))
         return;

      var owner = GetOwnerWindow();
      if (owner is null)
         return;

      var dialog = new StyledDialog("Super Dev", "That repository link could not be opened.");
      await dialog.ShowDialog(owner);
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

   private void CommitRepository()
   {
      var url = RepositoryBox.Text?.Trim() ?? string.Empty;
      if (RepositoryBox.Text != url)
         RepositoryBox.Text = url;

      OpenRepositoryButton.IsEnabled = ExternalLink.CanOpen(url);

      if (url == RepositoryUrl)
         return;

      RepositoryUrl = url;
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

   /// <summary>
   /// Shows or hides the template line, repository, branch, and PR fields.
   /// The title and progress stay visible. A later interface-density setting can call this.
   /// </summary>
   public void SetHeaderDetailsVisible(bool visible)
   {
      if (!visible)
         CommitDetails();

      _headerDetailsVisible = visible;
      HeaderDetails.IsVisible = visible;

      if (visible)
      {
         HeaderPanel.Classes.Remove("compact");
         HeaderToggleButton.Classes.Add("selected");
         ApplyDetailsVisibility();
      }
      else
      {
         HeaderPanel.Classes.Add("compact");
         HeaderToggleButton.Classes.Remove("selected");
      }

      HeaderToggleIcon.Data = Geometry.Parse(visible
          ? "M1,10 L7,4 L13,10"
          : "M1,4 L7,10 L13,4");
      ToolTip.SetTip(HeaderToggleButton, visible ? "Hide details" : "Show details");
   }

   private void HeaderToggle_Click(object? sender, RoutedEventArgs e) =>
       SetHeaderDetailsVisible(!_headerDetailsVisible);

   /// <summary>
   /// Shows or hides search, status, priority, and sort.
   /// A later interface-density setting can call this.
   /// </summary>
   public void SetFilterSectionVisible(bool visible)
   {
      _filtersVisible = visible;
      FiltersPanel.IsVisible = visible;
      ChecklistBody.RowSpacing = visible ? 14 : 0;
      FilterToggleButton.Content = visible ? "Hide filters" : "Show filters";

      if (_viewReady)
         ApplyView();
   }

   private void FilterToggle_Click(object? sender, RoutedEventArgs e) =>
       SetFilterSectionVisible(!_filtersVisible);

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
             entry.item.Notes.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             (entry.item.Deadline is { } deadline &&
              DeadlineValue.Format(deadline).Contains(search, StringComparison.OrdinalIgnoreCase)));
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
      var count = narrowed ? $"{shown} of {total}" : string.Empty;
      ViewCountText.Text = count;
      ViewCountText.IsVisible = narrowed && _filtersVisible;
      CollapsedCountText.Text = count;
      CollapsedCountText.IsVisible = narrowed && !_filtersVisible;
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

   private void AddCheck_Click(object? sender, RoutedEventArgs e) =>
       AddCheck();

   public void AddCheck()
   {
      _ = AddCheckAsync();
   }

   private async Task AddCheckAsync()
   {
      var owner = GetOwnerWindow();
      if (owner is null)
         return;

      var dialog = new AddCheckWindow();
      var result = await dialog.ShowDialog<ChecklistItem?>(owner);

      if (result is null)
         return;

      _checklist.Insert(0, result);
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
      item.Deadline = result.Deadline;
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

   private void DeleteChecks_Click(object? sender, RoutedEventArgs e) =>
       DeleteChecks();

   public void DeleteChecks()
   {
      _ = DeleteChecksAsync();
   }

   private async Task DeleteChecksAsync()
   {
      var owner = GetOwnerWindow();
      if (owner is null)
         return;

      var checks = _checklist.ToList();
      var picker = new DeleteChecksWindow(checks.Select(CopyItemLabel).ToList());
      var indexes = await picker.ShowDialog<List<int>?>(owner);
      if (indexes is null || indexes.Count == 0)
         return;

      var selected = indexes
          .Where(index => index >= 0 && index < checks.Count)
          .Distinct()
          .Select(index => checks[index])
          .ToList();
      if (selected.Count == 0)
         return;

      var message = selected.Count == 1
          ? $"Are you sure you want to remove \"{selected[0].Title}\"?"
          : $"Are you sure you want to remove these {selected.Count} checks?";

      var confirm = new StyledDialog(
          "Remove checklist items?",
          message,
          "Remove",
          "Cancel",
          danger: true);

      if (!await confirm.ShowDialog<bool>(owner))
         return;

      RemoveItems(selected);
   }

   private static string CopyItemLabel(ChecklistItem item) =>
       string.IsNullOrWhiteSpace(item.Detail)
           ? item.Title
           : $"{item.Title} — {item.Detail}";

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
