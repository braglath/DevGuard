using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DevGuard.Views;

public partial class StyledDialog : Window
{
   private readonly bool _isConfirmation;

   public StyledDialog(
       string title,
       string message)
   {
      InitializeComponent();

      _isConfirmation = false;

      DialogTitle.Text = title;
      DialogMessage.Text = message;

      SecondaryButton.IsVisible = false;

      PrimaryButton.Content = "OK";
   }

   public StyledDialog(
       string title,
       string message,
       string primaryText,
       string secondaryText,
       bool danger = false)
   {
      InitializeComponent();

      _isConfirmation = true;

      DialogTitle.Text = title;
      DialogMessage.Text = message;

      PrimaryButton.Content = primaryText;
      SecondaryButton.Content = secondaryText;

      if (danger)
      {
         PrimaryButton.Classes.Remove("primary");
         PrimaryButton.Classes.Add("danger");
      }
   }

   private void PrimaryButton_Click(
       object? sender,
       RoutedEventArgs e)
   {
      Close(true);
   }

   private void SecondaryButton_Click(
       object? sender,
       RoutedEventArgs e)
   {
      Close(false);
   }
}