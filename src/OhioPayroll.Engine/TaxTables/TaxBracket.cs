namespace OhioPayroll.Engine.TaxTables;

public record TaxBracket
{
    public decimal BracketStart { get; init; }
    public decimal BracketEnd { get; init; }
    public decimal Rate { get; init; }
    public decimal BaseAmount { get; init; }
}

