using Avalonia;
using Avalonia.Controls;

namespace SuperDev.Controls;

public class TextBoxClearButton
{
    public static readonly AttachedProperty<bool> ShowProperty =
        AvaloniaProperty.RegisterAttached<TextBoxClearButton, TextBox, bool>("Show");

    public static readonly AttachedProperty<bool> WhenFocusedProperty =
        AvaloniaProperty.RegisterAttached<TextBoxClearButton, TextBox, bool>("WhenFocused");

    static TextBoxClearButton()
    {
        ShowProperty.Changed.AddClassHandler<TextBox>(OnShowChanged);
        WhenFocusedProperty.Changed.AddClassHandler<TextBox>(OnWhenFocusedChanged);
    }

    public static bool GetShow(AvaloniaObject element) =>
        element.GetValue(ShowProperty);

    public static void SetShow(AvaloniaObject element, bool value) =>
        element.SetValue(ShowProperty, value);

    public static bool GetWhenFocused(AvaloniaObject element) =>
        element.GetValue(WhenFocusedProperty);

    public static void SetWhenFocused(AvaloniaObject element, bool value) =>
        element.SetValue(WhenFocusedProperty, value);

    private static void OnShowChanged(TextBox box, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is not true || box.InnerRightContent is not null)
            return;

        var button = new Button
        {
            Content = "×",
            Focusable = false,
            IsVisible = HasText(box)
        };
        button.Classes.Add("clear-text");
        ToolTip.SetTip(button, "Clear");

        button.Click += (_, _) =>
        {
            box.Text = string.Empty;
            box.Focus();
        };

        box.TextChanged += (_, _) => UpdateButton(box, button);
        box.GotFocus += (_, _) => UpdateButton(box, button);
        box.LostFocus += (_, _) => UpdateButton(box, button);

        box.InnerRightContent = button;
        UpdateButton(box, button);
    }

    private static void OnWhenFocusedChanged(TextBox box, AvaloniaPropertyChangedEventArgs args)
    {
        if (box.InnerRightContent is Button button)
            UpdateButton(box, button);
    }

    private static void UpdateButton(TextBox box, Button button)
    {
        var show = HasText(box);
        if (GetWhenFocused(box))
            show = show && box.IsFocused;

        button.IsVisible = show;
    }

    private static bool HasText(TextBox box) =>
        !string.IsNullOrEmpty(box.Text);
}
