using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Kingdom.World.Editor.Dialogs;

public enum DialogChoice
{
    None,
    Primary,
    Secondary,
    Cancel,
}

public sealed partial class ChoiceDialog : Window
{
    private readonly TextBlock _heading;
    private readonly TextBlock _message;
    private readonly Button _primary;
    private readonly Button _secondary;
    private readonly Button _cancel;

    public ChoiceDialog()
    {
        AvaloniaXamlLoader.Load(this);
        _heading = FindRequired<TextBlock>("HeadingText");
        _message = FindRequired<TextBlock>("MessageText");
        _primary = FindRequired<Button>("PrimaryButton");
        _secondary = FindRequired<Button>("SecondaryButton");
        _cancel = FindRequired<Button>("CancelButton");
    }

    public ChoiceDialog(
        string title,
        string heading,
        string message,
        string primaryText,
        string? secondaryText = null,
        string cancelText = "Cancel")
        : this()
    {
        Title = title;
        _heading.Text = heading;
        _message.Text = message;
        _primary.Content = primaryText;
        _secondary.Content = secondaryText;
        _secondary.IsVisible = !string.IsNullOrWhiteSpace(secondaryText);
        _cancel.Content = cancelText;
        _cancel.IsVisible = !string.IsNullOrWhiteSpace(cancelText);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close(DialogChoice.Cancel);
            e.Handled = true;
        }
    }

    private void Primary_OnClick(object? sender, RoutedEventArgs e) => Close(DialogChoice.Primary);

    private void Secondary_OnClick(object? sender, RoutedEventArgs e) => Close(DialogChoice.Secondary);

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(DialogChoice.Cancel);

    private T FindRequired<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"Required control '{name}' was not found.");
}
