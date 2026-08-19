namespace Gym.Core.Entities;

/// <summary>
/// A company the gym has a corporate agreement with (Module 4.6). Employees self-enrol with
/// the code rather than the desk keying them in one at a time, and HR gets a usage export
/// that proves the spend — which is what makes a corporate deal renew.
/// </summary>
public class CorporateAccount : BaseEntity
{
    public string CompanyName { get; set; } = string.Empty;
    /// <summary>Uppercase, unique, handed to employees: "ACME25".</summary>
    public string Code { get; set; } = string.Empty;
    public string? Domain { get; set; }

    public string HrContactName { get; set; } = string.Empty;
    public string HrContactEmail { get; set; } = string.Empty;
    public string? HrContactPhone { get; set; }

    /// <summary>Percentage off the branch price for anyone enrolling with this code.</summary>
    public decimal DiscountPercent { get; set; }
    /// <summary>Admission fee waived for corporate joiners — the usual sweetener.</summary>
    public bool WaiveAdmissionFee { get; set; } = true;
    /// <summary>Null = unlimited. Enrolment refuses once the cap is reached.</summary>
    public int? SeatCap { get; set; }
    public int SeatsUsed { get; set; }

    /// <summary>Null = every branch. Comma-separated branch ids otherwise.</summary>
    public string? BranchScope { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public ICollection<CorporateEnrolment> Enrolments { get; set; } = new List<CorporateEnrolment>();
}

/// <summary>
/// One employee on one corporate account. Kept as its own row rather than a flag on the
/// member so a member who leaves the company keeps their history and the seat is released.
/// </summary>
public class CorporateEnrolment : BaseEntity
{
    public int CorporateAccountId { get; set; }
    public CorporateAccount CorporateAccount { get; set; } = null!;
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public string? EmployeeId { get; set; }
    public string? WorkEmail { get; set; }
    public DateOnly EnrolledOn { get; set; }
    public DateOnly? EndedOn { get; set; }
    public bool IsActive { get; set; } = true;
}
