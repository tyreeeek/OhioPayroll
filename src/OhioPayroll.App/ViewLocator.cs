using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using OhioPayroll.App.Services;
using OhioPayroll.App.ViewModels;
using OhioPayroll.App.Views;

namespace OhioPayroll.App;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        try
        {
            var view = param switch
            {
                DashboardViewModel => (Control)new DashboardView(),
                SettingsViewModel => new SettingsView(),
                EmployeeListViewModel => new EmployeeListView(),
                ContractorListViewModel => new ContractorListView(),
                PayrollRunViewModel => new PayrollRunView(),
                ContractorPayrollViewModel => new ContractorPayrollView(),
                BankAccountsViewModel => new BankAccountsView(),
                CheckPrintingViewModel => new CheckPrintingView(),
                DirectDepositViewModel => new DirectDepositView(),
                QuarterlyViewModel => new QuarterlyView(),
                TaxLiabilityViewModel => new TaxLiabilityView(),
                ReportsViewModel => new ReportsView(),
                YearEndViewModel => new YearEndView(),
                _ => null
            };

            if (view is not null)
                return view;

            var typeName = param.GetType().Name;
            AppLogger.Warning($"ViewLocator: No view mapped for {typeName}");
            return new TextBlock
            {
                Text = $"No view for: {typeName}",
                Foreground = Brushes.Red,
                FontSize = 16
            };
        }
        catch (Exception ex)
        {
            AppLogger.Error($"ViewLocator error: {ex.Message}", ex);
            return new TextBlock
            {
                Text = $"View error: {ex.Message}",
                Foreground = Brushes.Red,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            };
        }
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}

