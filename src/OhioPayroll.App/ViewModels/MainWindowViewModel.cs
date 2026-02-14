using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OhioPayroll.App.Services;

namespace OhioPayroll.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    [ObservableProperty]
    private string _selectedSection = "Dashboard";

    public MainWindowViewModel(IServiceProvider services)
    {
        _services = services;
        NavigateTo("Dashboard");
    }

    [RelayCommand]
    private void NavigateTo(string section)
    {
        try
        {
            SelectedSection = section;
            CurrentViewModel = section switch
            {
                "Dashboard" => _services.GetRequiredService<DashboardViewModel>(),
                "Employees" => _services.GetRequiredService<EmployeeListViewModel>(),
                "Contractors" => _services.GetRequiredService<ContractorListViewModel>(),
                "Payroll" => _services.GetRequiredService<PayrollRunViewModel>(),
                "BankAccounts" => _services.GetRequiredService<BankAccountsViewModel>(),
                "CheckPrinting" => _services.GetRequiredService<CheckPrintingViewModel>(),
                "DirectDeposit" => _services.GetRequiredService<DirectDepositViewModel>(),
                "Quarterly" => _services.GetRequiredService<QuarterlyViewModel>(),
                "TaxLiability" => _services.GetRequiredService<TaxLiabilityViewModel>(),
                "Reports" => _services.GetRequiredService<ReportsViewModel>(),
                "YearEnd" => _services.GetRequiredService<YearEndViewModel>(),
                "Settings" => _services.GetRequiredService<SettingsViewModel>(),
                _ => CurrentViewModel
            };

            AppLogger.Information($"Navigated to {section}");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Navigation to {section} failed: {ex.Message}", ex);
        }
    }
}

