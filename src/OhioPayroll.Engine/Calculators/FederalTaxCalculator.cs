using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Engine.TaxTables;

namespace OhioPayroll.Engine.Calculators;

public class FederalTaxCalculator
{
    private readonly List<TaxBracket> _singleBrackets;
    private readonly List<TaxBracket> _marriedBrackets;
    private readonly List<TaxBracket> _hohBrackets;

    public FederalTaxCalculator(List<TaxBracket> singleBrackets, List<TaxBracket> marriedBrackets, List<TaxBracket> hohBrackets)
    {
        if (singleBrackets == null || singleBrackets.Count == 0)
            throw new InvalidOperationException("No federal tax brackets configured for Single filing status. Check that tax tables exist for the current tax year.");
        if (marriedBrackets == null || marriedBrackets.Count == 0)
            throw new InvalidOperationException("No federal tax brackets configured for Married filing status. Check that tax tables exist for the current tax year.");
        if (hohBrackets == null || hohBrackets.Count == 0)
            throw new InvalidOperationException("No federal tax brackets configured for Head of Household filing status. Check that tax tables exist for the current tax year.");
        _singleBrackets = singleBrackets.OrderBy(b => b.BracketStart).ToList();
        _marriedBrackets = marriedBrackets.OrderBy(b => b.BracketStart).ToList();
        _hohBrackets = hohBrackets.OrderBy(b => b.BracketStart).ToList();
    }

    /// <summary>
    /// IRS W-4 Step 4(b) "Other adjustments - Deductions" value per allowance claimed.
    /// </summary>
    private const decimal AllowanceDeduction = 4_300m;

    public decimal Calculate(
        decimal grossPay,
        FilingStatus filingStatus,
        PayFrequency frequency,
        int allowances = 0)
    {
        if (grossPay <= 0) return 0m;
        if (allowances < 0) throw new ArgumentOutOfRangeException(nameof(allowances), "Allowances cannot be negative.");

        int periods = (int)frequency;
        decimal annualizedWage = grossPay * periods;

        // Reduce taxable wages by allowance deductions (W-4 Step 4b)
        if (allowances > 0)
            annualizedWage = Math.Max(0m, annualizedWage - allowances * AllowanceDeduction);

        var brackets = filingStatus switch
        {
            FilingStatus.Single or FilingStatus.MarriedWithholdAtSingle => _singleBrackets,
            FilingStatus.Married => _marriedBrackets,
            FilingStatus.HeadOfHousehold => _hohBrackets,
            _ => _singleBrackets
        };

        decimal annualTax = ApplyBrackets(annualizedWage, brackets);
        decimal periodTax = Math.Round(annualTax / periods, 2, MidpointRounding.AwayFromZero);

        return Math.Max(0m, periodTax);
    }

    private static decimal ApplyBrackets(decimal annualWage, List<TaxBracket> brackets)
    {
        foreach (var bracket in brackets)
        {
            if (annualWage <= bracket.BracketEnd)
            {
                decimal taxableInBracket = annualWage - bracket.BracketStart;
                return bracket.BaseAmount + Math.Round(taxableInBracket * bracket.Rate, 2, MidpointRounding.AwayFromZero);
            }
        }

        var lastBracket = brackets[^1];
        decimal taxableAbove = annualWage - lastBracket.BracketStart;
        return lastBracket.BaseAmount + Math.Round(taxableAbove * lastBracket.Rate, 2, MidpointRounding.AwayFromZero);
    }
}

