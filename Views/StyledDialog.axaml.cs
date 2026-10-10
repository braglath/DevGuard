using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DevGuard.Views;

public partial class StyledDialog : Window
{

   public StyledDialog(
       string title,
       string message)
   {
      InitializeComponent();


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