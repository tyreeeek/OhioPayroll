using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OhioPayroll.App.Views;

/// <summary>
/// Base UserControl for views with a confirmation overlay (named "ConfirmOverlay").
/// Handles auto-focus when the overlay becomes visible, cleans up event handlers
/// on unload, and routes Enter/Escape keys to ConfirmActionCommand/CancelConfirmationCommand.
/// </summary>
public class ConfirmableUserControl : UserControl
{
    private Border? _overlay;
    private EventHandler<AvaloniaPropertyChangedEventArgs>? _overlayHandler;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _overlay = this.FindControl<Border>("ConfirmOverlay");
        if (_overlay is not null)
        {
            _overlayHandler = (_, args) =>
            {
                if (args.Property.Name == nameof(Border.IsVisible) && _overlay.IsVisible)
                {
                    _overlay.Focus();
                }
            };
            _overlay.PropertyChanged += _overlayHandler;
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        if (_overlay is not null && _overlayHandler is not null)
        {
            _overlay.PropertyChanged -= _overlayHandler;
            _overlayHandler = null;
            _overlay = null;
        }

        base.OnUnloaded(e);
    }

    protected void ConfirmOverlay_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is null) return;

        if (e.Key == Key.Escape)
        {
            if (TryExecuteCommand("CancelConfirmationCommand"))
                e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (TryExecuteCommand("ConfirmActionCommand"))
                e.Handled = true;
        }
    }

    private bool TryExecuteCommand(string commandPropertyName)
    {
        var prop = DataContext?.GetType().GetProperty(commandPropertyName);
        if (prop?.GetValue(DataContext) is ICommand cmd && cmd.CanExecute(null))
        {
            cmd.Execute(null);
            return true;
        }
        return false;
    }
}
