using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Engine.TaxTables;

namespace OhioPayroll.Engine.Calculators;

public class FederalTaxCalculator
{
    private readonly List<TaxBracket> _singleBrackets;
    private readonly List<TaxBracket> _marriedBrackets;

    public FederalTaxCalculator(List<TaxBracket> singleBrackets, List<TaxBracket> marriedBrackets)
    {
        if (singleBrackets == null || singleBrackets.Count == 0)
            throw new InvalidOperationException("No federal tax brackets configured for Single filing status. Check that tax tables exist for the current tax year.");
        if (marriedBrackets == null || marriedBrackets.Count == 0)
            throw new InvalidOperationException("No federal tax brackets configured for Married filing status. Check that tax tables exist for the current tax year.");
        _singleBrackets = singleBrackets.OrderBy(b => b.BracketStart).ToList();
        _marriedBrackets = marriedBrackets.OrderBy(b => b.BracketStart).ToList();
    }

    public decimal Calculate(
        decimal grossPay,
        FilingStatus filingStatus,
        PayFrequency frequency,
        int allowances)
    {
        if (grossPay <= 0) return 0m;

        int periods = (int)frequency;
        decimal annualizedWage = grossPay * periods;

        var brackets = filingStatus switch
        {
            FilingStatus.Single or FilingStatus.MarriedWithholdAtSingle => _singleBrackets,
            FilingStatus.Married => _marriedBrackets,
            FilingStatus.HeadOfHousehold => _singleBrackets,
            _ => _singleBrackets
        };

        decimal annualTax = ApplyBrackets(annualizedWage, brackets);
        decimal periodTax = Math.Round(annualTax / periods, 2);

        return Math.Max(0m, periodTax);
    }

    private static decimal ApplyBrackets(decimal annualWage, List<TaxBracket> brackets)
    {
        foreach (var bracket in brackets)
        {
            if (annualWage <= bracket.BracketEnd)
            {
                decimal taxableInBracket = annualWage - bracket.BracketStart;
                return bracket.BaseAmount + Math.Round(taxableInBracket * bracket.Rate, 2);
            }
        }

        var lastBracket = brackets[^1];
        decimal taxableAbove = annualWage - lastBracket.BracketStart;
        return lastBracket.BaseAmount + Math.Round(taxableAbove * lastBracket.Rate, 2);
    }
}
