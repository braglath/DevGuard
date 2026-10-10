
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DevGuard.Models;

namespace DevGuard.Views;

public partial class ChecklistView : UserControl
{
   private readonly WorkType _workType;
   private readonly ObservableCollection<ChecklistItem> _checklist;
   public string WorkTitle { get; }

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
       List<ChecklistItem> checklist)
   {
      InitializeComponent();

      _workType = workType;
      WorkTitle = workTitle;
      _checklist = new ObservableCollection<ChecklistItem>(checklist);

      WorkflowTitle.Text = workTitle;
      WorkflowDescription.Text = GetWorkflowDescription(workType);
      ChecklistItems.ItemsSource = _checklist;

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
      DocumentEdited?.Invoke(this, EventArgs.Empty);
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

      ChecklistItems.ItemsSource = null;
      ChecklistItems.ItemsSource = _checklist;

      MarkEdited();
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
