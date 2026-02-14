using FluentAssertions;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Engine.Calculators;

namespace OhioPayroll.Engine.Tests;

public class GrossPayCalculatorTests
{
    [Fact]
    public void Hourly_40Hours_NoOvertime_CalculatesCorrectly()
    {
        var (regular, overtime, gross) = GrossPayCalculator.Calculate(
            PayType.Hourly, 25.00m, 0, 40, 0, PayFrequency.BiWeekly);

        regular.Should().Be(1000.00m);
        overtime.Should().Be(0m);
        gross.Should().Be(1000.00m);
    }

    [Fact]
    public void Hourly_WithOvertime_CalculatesTimeAndHalf()
    {
        var (regular, overtime, gross) = GrossPayCalculator.Calculate(
            PayType.Hourly, 20.00m, 0, 40, 5, PayFrequency.BiWeekly);

        regular.Should().Be(800.00m);
        overtime.Should().Be(150.00m);
        gross.Should().Be(950.00m);
    }

    [Fact]
    public void Salary_BiWeekly_DividesBy26()
    {
        var (regular, overtime, gross) = GrossPayCalculator.Calculate(
            PayType.Salary, 0, 52_000m, 0, 0, PayFrequency.BiWeekly);

        regular.Should().Be(2_000.00m);
        overtime.Should().Be(0m);
        gross.Should().Be(2_000.00m);
    }

    [Fact]
    public void Salary_Weekly_DividesBy52()
    {
        var (regular, overtime, gross) = GrossPayCalculator.Calculate(
            PayType.Salary, 0, 52_000m, 0, 0, PayFrequency.Weekly);

        regular.Should().Be(1_000.00m);
        gross.Should().Be(1_000.00m);
    }

    [Fact]
    public void Salary_SemiMonthly_DividesBy24()
    {
        var (regular, overtime, gross) = GrossPayCalculator.Calculate(
            PayType.Salary, 0, 60_000m, 0, 0, PayFrequency.SemiMonthly);

        regular.Should().Be(2_500.00m);
        gross.Should().Be(2_500.00m);
    }

    [Fact]
    public void Salary_Monthly_DividesBy12()
    {
        var (regular, overtime, gross) = GrossPayCalculator.Calculate(
            PayType.Salary, 0, 60_000m, 0, 0, PayFrequency.Monthly);

        regular.Should().Be(5_000.00m);
        gross.Should().Be(5_000.00m);
    }

    [Fact]
    public void Hourly_ZeroHours_ReturnsZero()
    {
        var (regular, overtime, gross) = GrossPayCalculator.Calculate(
            PayType.Hourly, 25.00m, 0, 0, 0, PayFrequency.BiWeekly);

        gross.Should().Be(0m);
    }

    [Fact]
    public void Hourly_FractionalHours_RoundsToTwoDecimals()
    {
        var (regular, overtime, gross) = GrossPayCalculator.Calculate(
            PayType.Hourly, 15.33m, 0, 37.5m, 0, PayFrequency.BiWeekly);

        regular.Should().Be(574.88m);
    }
}
