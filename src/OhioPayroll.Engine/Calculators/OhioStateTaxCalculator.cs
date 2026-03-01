using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Engine.TaxTables;

namespace OhioPayroll.Engine.Calculators;

public class OhioStateTaxCalculator
{
    private readonly List<TaxBracket> _brackets;
    private const decimal PersonalExemptionValue = 2_400m;

    public OhioStateTaxCalculator(List<TaxBracket> brackets)
    {
        if (brackets == null || brackets.Count == 0)
            throw new InvalidOperationException("No Ohio tax brackets configured. Check that tax tables exist for the current tax year.");
        _brackets = brackets.OrderBy(b => b.BracketStart).ToList();
    }

    public decimal Calculate(
        decimal grossPay,
        PayFrequency frequency,
        int exemptions)
    {
        if (grossPay <= 0) return 0m;

        int periods = (int)frequency;
        decimal annualizedWage = grossPay * periods;

        decimal exemptionAmount = exemptions * PersonalExemptionValue;
        decimal taxableWage = Math.Max(0, annualizedWage - exemptionAmount);

        decimal annualTax = ApplyBrackets(taxableWage);
        return Math.Round(annualTax / periods, 2, MidpointRounding.AwayFromZero);
    }

    private decimal ApplyBrackets(decimal taxableWage)
    {
        foreach (var bracket in _brackets)
        {
            if (taxableWage <= bracket.BracketEnd)
            {
                decimal taxableInBracket = taxableWage - bracket.BracketStart;
                if (taxableInBracket < 0) return 0m;
                return Math.Round(bracket.BaseAmount + taxableInBracket * bracket.Rate, 2, MidpointRounding.AwayFromZero);
            }
        }

        var lastBracket = _brackets[^1];
        decimal taxableAbove = taxableWage - lastBracket.BracketStart;
        return Math.Round(lastBracket.BaseAmount + taxableAbove * lastBracket.Rate, 2, MidpointRounding.AwayFromZero);
    }
}

