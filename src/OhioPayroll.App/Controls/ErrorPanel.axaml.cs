using Avalonia;
using Avalonia.Controls;

namespace OhioPayroll.App.Controls;

/// <summary>
/// Reusable error panel control that displays errors with context and recovery options.
/// Provides consistent error UX across the application.
/// </summary>
public partial class ErrorPanel : UserControl
{
    /// <summary>
    /// Styled property for the error message text.
    /// </summary>
    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<ErrorPanel, string?>(
            nameof(ErrorMessage),
            defaultValue: null);

    /// <summary>
    /// Styled property for optional error context/help text.
    /// </summary>
    public static readonly StyledProperty<string?> ErrorContextProperty =
        AvaloniaProperty.Register<ErrorPanel, string?>(
            nameof(ErrorContext),
            defaultValue: null);

    /// <summary>
    /// Gets or sets the error message to display.
    /// Panel is hidden when this is null or empty.
    /// </summary>
    public string? ErrorMessage
    {
        get => GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    /// <summary>
    /// Gets or sets optional context or help text for the error.
    /// </summary>
    public string? ErrorContext
    {
        get => GetValue(ErrorContextProperty);
        set => SetValue(ErrorContextProperty, value);
    }

    public ErrorPanel()
    {
        InitializeComponent();
        // Note: DataContext inherited from parent ViewModel for command bindings
        //       ErrorMessage/ErrorContext bind to this control's properties via #ControlName
    }
}
