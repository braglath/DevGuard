using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SuperDev.Models;

namespace SuperDev.Views;

public partial class AddCheckWindow : Window
{
   private readonly ChecklistItem? _editingItem;
   private double _compactHeight;
   private bool _notesExpanded;
   private bool _deadlineOpening;
   private DateOnly? _deadline;

   public AddCheckWindow()
   {
      InitializeComponent();

      Title = "Add Check";

      CategoryComboBox.SelectedIndex = 0;
      PriorityComboBox.SelectedIndex = 1;
   }

   protected override void OnOpened(EventArgs e)
   {
      base.OnOpened(e);

      if (_editingItem is null)
         TitleTextBox.Focus();
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
      _deadline = item.Deadline;
      ApplyDeadlineLink();
   }

   private void DeadlineFlyout_Opened(object? sender, EventArgs e)
   {
      _deadlineOpening = true;
      var selected = _deadline is { } date ? DeadlineValue.ToCalendar(date) : (DateTime?)null;
      DeadlineCalendar.SelectedDate = selected;
      DeadlineCalendar.DisplayDate = selected ?? DateTime.Today;
      _deadlineOpening = false;
   }

   private void DeadlineCalendar_Changed(object? sender, SelectionChangedEventArgs e)
   {
      if (_deadlineOpening || DeadlineCalendar.SelectedDate is not { } selected)
         return;

      _deadline = DeadlineValue.FromCalendar(selected);
      ApplyDeadlineLink();
      DeadlineButton.Flyout?.Hide();
   }

   private void ClearDeadline_Click(object? sender, RoutedEventArgs e)
   {
      _deadline = null;
      ApplyDeadlineLink();
   }

   private void ApplyDeadlineLink()
   {
      DeadlineButton.Content = _deadline is { } date
          ? $"Deadline: {DeadlineValue.Format(date)}"
          : "Pick deadline";
      ClearDeadlineButton.IsVisible = _deadline is not null;
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

   private void ToggleNotes_Click(object? sender, RoutedEventArgs e)
   {
      if (_notesExpanded)
      {
         SetHeightWithinScreen(_compactHeight);
         ExpandNotesButton.Content = "Expand";
         _notesExpanded = false;
         return;
      }

      _compactHeight = Height;
      SetHeightWithinScreen(Math.Max(Height + 280, 760));
      ExpandNotesButton.Content = "Collapse";
      _notesExpanded = true;
      NotesTextBox.Focus();
   }

   private void SetHeightWithinScreen(double height)
   {
      var screen = Screens?.ScreenFromWindow(this);
      if (screen is null || screen.Scaling <= 0)
      {
         Height = height;
         return;
      }

      var scale = screen.Scaling;
      var work = screen.WorkingArea;
      var chrome = FrameChromeDip();
      var maxHeight = Math.Max(MinHeight, work.Height / scale - chrome);
      Height = Math.Clamp(height, MinHeight, maxHeight);

      var frameHeightPx = (int)Math.Ceiling((Height + chrome) * scale);
      var top = Position.Y;
      var bottomLimit = work.Y + work.Height;
      if (top + frameHeightPx > bottomLimit)
         top = bottomLimit - frameHeightPx;
      if (top < work.Y)
         top = work.Y;

      if (top != Position.Y)
         Position = new PixelPoint(Position.X, top);
   }

   private double FrameChromeDip()
   {
      if (FrameSize is { } frame && ClientSize.Height > 0)
         return Math.Max(0, frame.Height - ClientSize.Height) + 12;

      return 56;
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
         Deadline = _deadline,
         IsCompleted = _editingItem?.IsCompleted ?? false
      };

      Close(item);
   }
}