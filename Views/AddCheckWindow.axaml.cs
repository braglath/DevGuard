using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DevGuard.Models;

namespace DevGuard.Views;

public partial class AddCheckWindow : Window
{
   private readonly ChecklistItem? _editingItem;

   public AddCheckWindow()
   {
      InitializeComponent();

      Title = "Add Check";

      CategoryComboBox.SelectedIndex = 0;
      PriorityComboBox.SelectedIndex = 1;
   }

   public AddCheckWindow(ChecklistItem item)
   {
      InitializeComponent();

      Title = "Edit Check";

      _editingItem = item;

      TitleTextBox.Text = item.Title;

      SelectComboBoxItem(
          CategoryComboBox,
          item.Category);

      PriorityComboBox.SelectedIndex =
          (int)item.Priority;

      NotesTextBox.Text = item.Notes;
   }

   private void SelectComboBoxItem(
       ComboBox comboBox,
       string value)
   {
      foreach (var item in comboBox.Items)
      {
         if (item is ComboBoxItem comboItem &&
             comboItem.Content?.ToString() == value)
         {
            comboBox.SelectedItem = comboItem;
            return;
         }
      }
   }

   private void Cancel_Click(
       object? sender,
       RoutedEventArgs e)
   {
      Close(null);
   }

   private void Save_Click(
       object? sender,
       RoutedEventArgs e)
   {
      var title = TitleTextBox.Text?.Trim();
      

      if (string.IsNullOrWhiteSpace(title))
         return;

      var category =
          (CategoryComboBox.SelectedItem as ComboBoxItem)
              ?.Content?.ToString()
          ?? "Development";

      var priority =
          (ChecklistPriority)PriorityComboBox.SelectedIndex;

      var notes = NotesTextBox.Text?.Trim() ?? string.Empty;

      var item = new ChecklistItem
      {
         Id = _editingItem?.Id ?? Guid.NewGuid(),
         Title = title,
         Category = category,
         Priority = priority,
         Notes = notes,
         IsCompleted = _editingItem?.IsCompleted ?? false
      };

      Close(item);
   }
}