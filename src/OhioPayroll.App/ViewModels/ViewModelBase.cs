using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OhioPayroll.App.Services;

namespace OhioPayroll.App.ViewModels;

/// <summary>
/// Base class for all ViewModels providing common loading state management.
/// Ensures consistent UX for async operations across the application.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Indicates whether an async operation is currently in progress.
    /// Bind this to show/hide loading indicators in the UI.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Message describing the current operation (e.g., "Loading employees...").
    /// Displayed alongside loading indicators.
    /// </summary>
    [ObservableProperty]
    private string _loadingMessage = "Loading...";

    /// <summary>
    /// Error message from the most recent failed operation.
    /// Bind this to error display components in the UI.
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Optional context or help text for the error.
    /// Provides additional information to help the user resolve the issue.
    /// </summary>
    [ObservableProperty]
    private string? _errorContext;

    /// <summary>
    /// Indicates whether a retry action is available for the current error.
    /// Used to show/hide the Retry button in error displays.
    /// </summary>
    [ObservableProperty]
    private bool _canRetry;

    /// <summary>
    /// The retry action to execute when the user clicks Retry.
    /// </summary>
    private Func<Task>? _retryAction;

    /// <summary>
    /// Executes an async operation with automatic loading state management.
    /// Sets IsLoading=true, executes the operation, handles errors, and sets IsLoading=false.
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operation">The async operation to execute</param>
    /// <param name="loadingMessage">Message to display during operation (e.g., "Processing...")</param>
    /// <param name="suppressErrorDisplay">If true, errors are logged but not shown in ErrorMessage</param>
    /// <returns>The result of the operation, or default(T) if it fails</returns>
    protected async Task<T?> ExecuteWithLoadingAsync<T>(
        Func<Task<T>> operation,
        string loadingMessage = "Processing...",
        bool suppressErrorDisplay = false)
    {
        IsLoading = true;
        LoadingMessage = loadingMessage;
        ErrorMessage = null;

        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error: {ex.Message}";
            AppLogger.Error($"{loadingMessage} failed: {ex.Message}", ex);

            if (!suppressErrorDisplay)
                ErrorMessage = errorMsg;

            return default;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Executes an async operation (no return value) with automatic loading state management.
    /// Sets IsLoading=true, executes the operation, handles errors, and sets IsLoading=false.
    /// </summary>
    /// <param name="operation">The async operation to execute</param>
    /// <param name="loadingMessage">Message to display during operation</param>
    /// <param name="suppressErrorDisplay">If true, errors are logged but not shown in ErrorMessage</param>
    /// <returns>True if successful, false if failed</returns>
    protected async Task<bool> ExecuteWithLoadingAsync(
        Func<Task> operation,
        string loadingMessage = "Processing...",
        bool suppressErrorDisplay = false)
    {
        IsLoading = true;
        LoadingMessage = loadingMessage;
        ErrorMessage = null;

        try
        {
            await operation();
            return true;
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error: {ex.Message}";
            AppLogger.Error($"{loadingMessage} failed: {ex.Message}", ex);

            if (!suppressErrorDisplay)
                ErrorMessage = errorMsg;

            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Clears any displayed error message.
    /// Call this when the user dismisses an error or starts a new operation.
    /// </summary>
    protected void ClearError()
    {
        ErrorMessage = null;
        ErrorContext = null;
        CanRetry = false;
        _retryAction = null;
    }

    /// <summary>
    /// Shows an error message with optional context and retry action.
    /// </summary>
    /// <param name="message">The error message to display</param>
    /// <param name="context">Optional context or help text</param>
    /// <param name="retryAction">Optional action to execute when user clicks Retry</param>
    protected void ShowError(string message, string? context = null, Func<Task>? retryAction = null)
    {
        ErrorMessage = message;
        ErrorContext = context;
        CanRetry = retryAction != null;
        _retryAction = retryAction;
    }

    /// <summary>
    /// Command to retry the failed operation.
    /// Executes the retry action stored from ShowError.
    /// </summary>
    [RelayCommand]
    private async Task RetryAsync()
    {
        if (_retryAction != null)
        {
            ClearError();
            await _retryAction();
        }
    }

    /// <summary>
    /// Command to dismiss the current error message.
    /// Clears all error state.
    /// </summary>
    [RelayCommand]
    private void DismissError()
    {
        ClearError();
    }
}

