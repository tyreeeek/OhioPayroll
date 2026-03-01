using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using OhioPayroll.App.ViewModels;

namespace OhioPayroll.App.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _currentViewModel;
    private bool _isDarkTheme = true; // Start with dark theme (matches App.axaml RequestedThemeVariant="Dark")

    public MainWindow()
    {
        InitializeComponent();

        // Apply initial theme
        ApplyTheme();

        DataContextChanged += (_, _) =>
        {
            if (_currentViewModel is not null)
                _currentViewModel.PropertyChanged -= OnViewModelPropertyChanged;

            _currentViewModel = DataContext as MainWindowViewModel;

            if (_currentViewModel is not null)
            {
                _currentViewModel.PropertyChanged += OnViewModelPropertyChanged;
                UpdateActiveNav(_currentViewModel.SelectedSection);
            }
        };
    }

    private void ToggleTheme(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isDarkTheme = !_isDarkTheme;
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        if (_isDarkTheme)
        {
            Classes.Add("dark-theme");
            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        }
        else
        {
            Classes.Remove("dark-theme");
            Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_currentViewModel is not null)
        {
            _currentViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _currentViewModel = null;
        }

        base.OnClosed(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedSection) && sender is MainWindowViewModel vm)
        {
            UpdateActiveNav(vm.SelectedSection);
        }
    }

    private void UpdateActiveNav(string section)
    {
        var sidebar = this.FindControl<StackPanel>("NavPanel");
        if (sidebar is null) return;

        foreach (var child in sidebar.Children)
        {
            if (child is Button btn && btn.Classes.Contains("nav-item"))
            {
                if (btn.CommandParameter is string param && param == section)
                    btn.Classes.Add("nav-active");
                else
                    btn.Classes.Remove("nav-active");
            }
        }
    }
}
