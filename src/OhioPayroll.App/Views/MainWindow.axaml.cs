using System.ComponentModel;
using Avalonia.Controls;
using OhioPayroll.App.ViewModels;

namespace OhioPayroll.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
                UpdateActiveNav(vm.SelectedSection);
            }
        };
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
