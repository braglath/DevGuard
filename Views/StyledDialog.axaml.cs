using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SuperDev.Views;

public enum ClosePromptResult
{
   Cancel = 0,
   Save = 1,
   Discard = 2
}

public partial class StyledDialog : Window
{
   private bool _choiceDialog;

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

   public StyledDialog(
       string title,
       string message,
       string saveText,
       string cancelText,
       string discardText)
   {
      InitializeComponent();

      _choiceDialog = true;
      Width = 560;

      DialogTitle.Text = title;
      DialogMessage.Text = message;

      TertiaryButton.IsVisible = true;
      TertiaryButton.Content = discardText;
      SecondaryButton.Content = cancelText;
      PrimaryButton.Content = saveText;
   }

   private void PrimaryButton_Click(
       object? sender,
       RoutedEventArgs e)
   {
      if (_choiceDialog)
         Close(ClosePromptResult.Save);
      else
         Close(true);
   }

   private void SecondaryButton_Click(
       object? sender,
       RoutedEventArgs e)
   {
      if (_choiceDialog)
         Close(ClosePromptResult.Cancel);
      else
         Close(false);
   }

   private void TertiaryButton_Click(
       object? sender,
       RoutedEventArgs e)
   {
      Close(ClosePromptResult.Discard);
   }
}