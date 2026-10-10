
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

      UpdateProgress();
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
      UpdateProgress();
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

      UpdateProgress();
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
      UpdateProgress();
   }
   private async void Complete_Click(object? sender, RoutedEventArgs e)
   {
      var incompleteRequired = _checklist
          .Where(x =>
              !x.IsCompleted &&
              (x.Priority == ChecklistPriority.Critical ||
               x.Priority == ChecklistPriority.Required))
          .ToList();

      var owner = GetOwnerWindow();
      if (owner is null)
         return;

      if (incompleteRequired.Count > 0)
      {
         var dialog = new StyledDialog(
             "Checklist incomplete",
             $"You still have {incompleteRequired.Count} " +
             "Critical/Required check(s) incomplete.");

         await dialog.ShowDialog(owner);
         return;
      }

      var successDialog = new StyledDialog(
          "Checklist complete",
          "All required checks have been completed.");

      await successDialog.ShowDialog(owner);
   }
}
