using Avalonia;
using Avalonia.Controls;

namespace OhioPayroll.App.Controls;

/// <summary>
/// Reusable loading overlay control that displays a spinner and message
/// over the entire view during async operations.
/// </summary>
public partial class LoadingOverlay : UserControl
{
    /// <summary>
    /// Styled property for the loading message text.
    /// </summary>
    public static readonly StyledProperty<string> LoadingMessageProperty =
        AvaloniaProperty.Register<LoadingOverlay, string>(
            nameof(LoadingMessage),
            defaultValue: "Loading...");

    /// <summary>
    /// Gets or sets the message displayed during loading.
    /// </summary>
    public string LoadingMessage
    {
        get => GetValue(LoadingMessageProperty);
        set => SetValue(LoadingMessageProperty, value);
    }

    public LoadingOverlay()
    {
        InitializeComponent();
        DataContext = this;
    }
}
