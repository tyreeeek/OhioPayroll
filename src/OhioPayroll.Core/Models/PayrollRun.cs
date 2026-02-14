using OhioPayroll.Core.Models.Enums;

namespace OhioPayroll.Core.Models;

public class PayrollRun
{
    public int Id { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime PayDate { get; set; }
    public PayFrequency PayFrequency { get; set; }
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;
    public decimal TotalGrossPay { get; set; }
    public decimal TotalNetPay { get; set; }
    public decimal TotalFederalTax { get; set; }
    public decimal TotalStateTax { get; set; }
    public decimal TotalLocalTax { get; set; }
    public decimal TotalSocialSecurity { get; set; }
    public decimal TotalMedicare { get; set; }
    public decimal TotalEmployerSocialSecurity { get; set; }
    public decimal TotalEmployerMedicare { get; set; }
    public decimal TotalEmployerFuta { get; set; }
    public decimal TotalEmployerSuta { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FinalizedAt { get; set; }

    public ICollection<Paycheck> Paychecks { get; set; } = new List<Paycheck>();
}
