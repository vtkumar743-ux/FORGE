namespace Gym.Infrastructure.Services;

/// <summary>
/// GST arithmetic for a Karnataka supplier billing Karnataka customers: intra-state, so the
/// rate splits into equal CGST and SGST halves. IGST is computed instead when the place of
/// supply is another state, which is what a corporate customer with an out-of-state GSTIN
/// gets. Prices across the product are quoted GST-inclusive — that is what an Indian gym
/// puts on a pricing page — so <see cref="FromGross"/> is the primary entry point and
/// <see cref="FromNet"/> exists for POS lines that are priced ex-tax.
/// </summary>
public static class GstCalculator
{
    public readonly record struct Split(
        decimal TaxableValue,
        decimal Cgst,
        decimal Sgst,
        decimal Igst,
        decimal TaxTotal,
        decimal Gross);

    /// <summary>Back-computes the taxable value out of a tax-inclusive amount.</summary>
    public static Split FromGross(decimal gross, decimal ratePercent, bool interState = false)
    {
        if (ratePercent <= 0) return new Split(Round(gross), 0, 0, 0, 0, Round(gross));

        var taxable = Round(gross / (1 + ratePercent / 100m));
        var tax = Round(gross - taxable);
        return Build(taxable, tax, interState, Round(gross));
    }

    /// <summary>Adds tax on top of an ex-tax amount.</summary>
    public static Split FromNet(decimal net, decimal ratePercent, bool interState = false)
    {
        var taxable = Round(net);
        var tax = Round(taxable * ratePercent / 100m);
        return Build(taxable, tax, interState, Round(taxable + tax));
    }

    private static Split Build(decimal taxable, decimal tax, bool interState, decimal gross)
    {
        if (interState) return new Split(taxable, 0, 0, tax, tax, gross);

        // The halves must add back to the total exactly; give the remainder paisa to SGST.
        var half = Round(tax / 2m);
        return new Split(taxable, half, tax - half, 0, tax, gross);
    }

    /// <summary>
    /// Invoices are collected in whole rupees at the desk, so the grand total rounds and the
    /// difference is carried as the statutory round-off line rather than silently absorbed.
    /// </summary>
    public static (decimal GrandTotal, decimal RoundOff) ApplyRoundOff(decimal amount)
    {
        var rounded = decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
        return (rounded, Round(rounded - amount));
    }

    public static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>"29-Karnataka" — the place-of-supply string GST invoices must carry.</summary>
    public static string PlaceOfSupply(string state) => state switch
    {
        "Karnataka" => "29-Karnataka",
        "Maharashtra" => "27-Maharashtra",
        "Tamil Nadu" => "33-Tamil Nadu",
        "Telangana" => "36-Telangana",
        "Delhi" => "07-Delhi",
        _ => state
    };

    /// <summary>A GSTIN's first two digits are its state code; different code ⇒ inter-state.</summary>
    public static bool IsInterState(string? supplierGstin, string? customerGstin)
    {
        if (string.IsNullOrWhiteSpace(supplierGstin) || string.IsNullOrWhiteSpace(customerGstin)) return false;
        if (supplierGstin.Length < 2 || customerGstin.Length < 2) return false;
        return supplierGstin[..2] != customerGstin[..2];
    }
}
