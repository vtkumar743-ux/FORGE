using Gym.Core.Entities;
using Gym.Core.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gym.Infrastructure.Persistence.Seeding;

/// <summary>
/// The 200-member demo roster, plus the billing, attendance and engagement history that
/// makes the admin dashboards read as a live business rather than an empty shell.
/// </summary>
internal static class MemberSeed
{
    public const int MemberCount = 200;
    /// <summary>Shared demo password for every seeded member; surfaced in the seeder banner.</summary>
    public const string DemoPassword = "Member@12345";

    private static readonly Dictionary<string, string> CodePrefix = new()
    {
        ["koramangala"] = "KOR", ["indiranagar"] = "IND", ["whitefield"] = "WHF"
    };

    public static async Task<List<Member>> SeedAsync(
        GymDbContext db, UserManager<ApplicationUser> users,
        List<Branch> branches, List<Plan> plans, DateOnly today, Random rng, CancellationToken ct)
    {
        if (await db.Members.AnyAsync(ct))
            return await db.Members.ToListAsync(ct);

        // Branch weighting: Koramangala is the original floor, Whitefield the biggest.
        var weights = new[] { ("koramangala", 0.40), ("indiranagar", 0.25), ("whitefield", 0.35) };
        var branchBySlug = branches.ToDictionary(b => b.Slug);
        var planBySlug = plans.ToDictionary(p => p.Slug);
        var branchPrices = await db.PlanBranchPrices.AsNoTracking().ToListAsync(ct);

        var members = new List<Member>(MemberCount);
        var usedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var perBranchCounter = branches.ToDictionary(b => b.Slug, _ => 0);

        for (var i = 0; i < MemberCount; i++)
        {
            var isFemale = rng.NextDouble() < 0.42;
            var first = isFemale
                ? SeedPools.FemaleFirstNames[rng.Next(SeedPools.FemaleFirstNames.Length)]
                : SeedPools.MaleFirstNames[rng.Next(SeedPools.MaleFirstNames.Length)];
            var last = SeedPools.LastNames[rng.Next(SeedPools.LastNames.Length)];
            var fullName = $"{first} {last}";

            var slug = PickWeighted(weights, rng);
            var branch = branchBySlug[slug];
            var seq = ++perBranchCounter[slug];

            var email = UniqueEmail(first, last, usedEmails);
            var phone = $"+919{rng.Next(100000000, 999999999)}";
            var (locality, pincode) = SeedPools.Localities[rng.Next(SeedPools.Localities.Length)];

            // Tenure drives everything downstream: status, streak, churn score, invoice count.
            var daysSinceJoin = WeightedTenureDays(rng);
            var joinedOn = today.AddDays(-daysSinceJoin);
            var status = PickStatus(daysSinceJoin, rng);

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                PhoneNumber = phone,
                PhoneNumberConfirmed = true,
                FullName = fullName,
                HomeBranchId = branch.Id,
                CreatedAtUtc = joinedOn.ToDateTime(new TimeOnly(10, 0)).AddMinutes(-330),
                LastLoginAtUtc = status is MemberStatus.Active or MemberStatus.Trial
                    ? DateTime.UtcNow.AddDays(-rng.Next(0, 14))
                    : DateTime.UtcNow.AddDays(-rng.Next(30, 200)),
                IsActive = status is not (MemberStatus.Cancelled or MemberStatus.Expired)
            };

            var created = await users.CreateAsync(user, DemoPassword);
            if (!created.Succeeded)
                throw new InvalidOperationException(
                    $"Seeding member '{email}' failed: {string.Join("; ", created.Errors.Select(e => e.Description))}");
            await users.AddToRoleAsync(user, RoleNames.Member);

            var member = new Member
            {
                UserId = user.Id,
                MemberCode = $"FRG-{CodePrefix[slug]}-{seq:D5}",
                HomeBranchId = branch.Id,
                FullName = fullName,
                Phone = phone,
                Email = email,
                DateOfBirth = today.AddYears(-rng.Next(19, 52)).AddDays(-rng.Next(0, 365)),
                Gender = isFemale ? Gender.Female : Gender.Male,
                PhotoUrl = null,
                AddressLine = $"{rng.Next(1, 320)}, {locality}",
                City = "Bengaluru",
                Pincode = pincode,
                Status = status,
                JoinedOn = joinedOn,
                PrimaryGoal = SeedPools.Goals[rng.Next(SeedPools.Goals.Length)],
                HeightCm = isFemale ? rng.Next(150, 172) : rng.Next(162, 187),
                StartWeightKg = isFemale ? rng.Next(48, 88) : rng.Next(58, 108),
                EmergencyContactName = $"{SeedPools.LastNames[rng.Next(SeedPools.LastNames.Length)]} family contact",
                EmergencyContactPhone = $"+919{rng.Next(100000000, 999999999)}",
                WaiverSigned = status != MemberStatus.Trial || rng.NextDouble() < 0.5,
                QrToken = QrToken(i),
                ReferralCode = $"{first.ToUpperInvariant()[..Math.Min(3, first.Length)]}{1000 + i}",
                ConsentMarketing = rng.NextDouble() < 0.86,
                ConsentLeaderboard = rng.NextDouble() < 0.55,
                ConsentTransformationShowcase = rng.NextDouble() < 0.22,
                CreatedAtUtc = user.CreatedAtUtc
            };
            if (member.WaiverSigned)
                member.WaiverSignedAtUtc = member.CreatedAtUtc.AddMinutes(20);

            // ~9% arrive on a corporate code — a real revenue channel in Bengaluru.
            if (rng.NextDouble() < 0.09)
                member.CorporateCode = slug == "whitefield" ? "CORPWIPRO" : "CORPFLIPKART";

            if (rng.NextDouble() < 0.12)
                member.Tags = SeedPools.MemberTagPool[rng.Next(SeedPools.MemberTagPool.Length)];

            members.Add(member);
        }

        db.Members.AddRange(members);
        await db.SaveChangesAsync(ct);

        // ~14% of members came in through another member's code.
        foreach (var member in members.Where(_ => rng.NextDouble() < 0.14))
        {
            var referrer = members[rng.Next(members.Count)];
            if (referrer.Id == member.Id) continue;
            member.ReferredByMemberId = referrer.Id;
        }
        await db.SaveChangesAsync(ct);

        await SubscriptionsAndBillingAsync(db, members, plans, planBySlug, branchPrices, today, rng, ct);
        return members;
    }

    private static async Task SubscriptionsAndBillingAsync(
        GymDbContext db, List<Member> members, List<Plan> plans,
        Dictionary<string, Plan> planBySlug, List<PlanBranchPrice> branchPrices,
        DateOnly today, Random rng, CancellationToken ct)
    {
        var sellablePlans = plans.Where(p => p.Kind is PlanKind.Recurring or PlanKind.FixedTerm).ToList();
        var subscriptions = new List<Subscription>();

        foreach (var member in members)
        {
            var plan = member.Status == MemberStatus.Trial
                ? planBySlug["monthly"]
                : PickPlan(sellablePlans, rng);

            var price = branchPrices
                .FirstOrDefault(p => p.PlanId == plan.Id && p.BranchId == member.HomeBranchId)?.Price ?? plan.BasePrice;

            // Anchor the term so Active plans are mid-term and Expired ones ended in the past.
            DateOnly startsOn = member.Status switch
            {
                MemberStatus.Expired => today.AddDays(-plan.DurationDays - rng.Next(5, 90)),
                MemberStatus.Cancelled => today.AddDays(-plan.DurationDays - rng.Next(10, 150)),
                MemberStatus.Trial => today.AddDays(-rng.Next(0, 6)),
                _ => today.AddDays(-rng.Next(1, Math.Max(2, plan.DurationDays - 3)))
            };
            if (startsOn < member.JoinedOn) startsOn = member.JoinedOn;

            var status = member.Status switch
            {
                MemberStatus.Frozen => SubscriptionStatus.Frozen,
                MemberStatus.Expired => SubscriptionStatus.Expired,
                MemberStatus.Cancelled => SubscriptionStatus.Cancelled,
                _ => SubscriptionStatus.Active
            };

            var discount = rng.NextDouble() < 0.18 ? decimal.Round(price * 0.1m, 0) : 0m;
            var sub = new Subscription
            {
                MemberId = member.Id,
                PlanId = plan.Id,
                BranchId = member.HomeBranchId,
                Status = status,
                StartsOn = startsOn,
                EndsOn = startsOn.AddDays(plan.DurationDays),
                PriceCharged = price - discount,
                DiscountAmount = discount,
                AutoRenew = plan.Kind == PlanKind.Recurring && rng.NextDouble() < 0.4,
                PtCreditsRemaining = plan.PtSessionCredits ?? 0,
                CreatedAtUtc = startsOn.ToDateTime(new TimeOnly(11, 0)).AddMinutes(-330)
            };
            if (sub.AutoRenew) sub.NextBillingOn = sub.EndsOn;
            if (status == SubscriptionStatus.Cancelled)
            {
                sub.CancelledOn = sub.EndsOn.AddDays(-rng.Next(1, 20));
                sub.CancellationReason = rng.Next(3) switch
                {
                    0 => "Relocated out of Bengaluru",
                    1 => "Switched to a gym closer to the office",
                    _ => "Cost"
                };
            }
            if (status == SubscriptionStatus.Frozen)
            {
                sub.FreezeStartsOn = today.AddDays(-rng.Next(2, 18));
                sub.FreezeEndsOn = sub.FreezeStartsOn.Value.AddDays(rng.Next(10, 30));
                sub.FreezeDaysUsed = today.DayNumber - sub.FreezeStartsOn.Value.DayNumber;
            }

            subscriptions.Add(sub);
        }

        db.Subscriptions.AddRange(subscriptions);
        await db.SaveChangesAsync(ct);

        await InvoicesAsync(db, members, subscriptions, plans, today, rng, ct);
    }

    private static async Task InvoicesAsync(
        GymDbContext db, List<Member> members, List<Subscription> subscriptions,
        List<Plan> plans, DateOnly today, Random rng, CancellationToken ct)
    {
        var planById = plans.ToDictionary(p => p.Id);
        var memberById = members.ToDictionary(m => m.Id);
        var invoices = new List<Invoice>();
        var payments = new List<Payment>();
        var sequence = 0;

        foreach (var sub in subscriptions)
        {
            var plan = planById[sub.PlanId];
            var member = memberById[sub.MemberId];
            var issuedOn = sub.StartsOn;
            var gross = sub.PriceCharged;

            // Gym services: 5% GST without ITC, effective 22 Sep 2025. Intra-state → CGST+SGST.
            var taxable = decimal.Round(gross / (1 + plan.GstRatePercent / 100m), 2);
            var tax = decimal.Round(gross - taxable, 2);
            var half = decimal.Round(tax / 2, 2);

            var status = ChooseInvoiceStatus(sub, today, rng);
            var invoice = new Invoice
            {
                InvoiceNumber = InvoiceNumber(issuedOn, ++sequence),
                MemberId = sub.MemberId,
                BranchId = sub.BranchId,
                SubscriptionId = sub.Id,
                IssuedOn = issuedOn,
                DueOn = issuedOn.AddDays(plan.Kind == PlanKind.Recurring ? 5 : 0),
                Status = status,
                SubTotal = decimal.Round(taxable + sub.DiscountAmount, 2),
                DiscountTotal = sub.DiscountAmount,
                TaxableValue = taxable,
                CgstAmount = half,
                SgstAmount = tax - half,
                GrandTotal = gross,
                SupplierGstin = "29AABCF4521K1ZP",
                PlaceOfSupply = "29-Karnataka",
                CreatedAtUtc = issuedOn.ToDateTime(new TimeOnly(11, 5)).AddMinutes(-330)
            };

            invoice.Lines.Add(new InvoiceLine
            {
                Description = $"{plan.Name} membership — {plan.DurationDays} days",
                SacOrHsnCode = plan.SacCode,
                Quantity = 1,
                UnitPrice = decimal.Round((taxable + sub.DiscountAmount) , 2),
                DiscountAmount = sub.DiscountAmount,
                TaxableValue = taxable,
                GstRatePercent = plan.GstRatePercent,
                CgstAmount = half,
                SgstAmount = tax - half,
                LineTotal = gross,
                PlanId = plan.Id
            });

            var paid = status switch
            {
                InvoiceStatus.Paid => gross,
                InvoiceStatus.PartiallyPaid => decimal.Round(gross * 0.5m, 0),
                _ => 0m
            };
            invoice.AmountPaid = paid;
            invoice.AmountDue = gross - paid;
            if (status == InvoiceStatus.Overdue)
            {
                invoice.RemindersSent = rng.Next(1, 4);
                invoice.LastReminderAtUtc = DateTime.UtcNow.AddDays(-rng.Next(1, 6));
            }

            invoices.Add(invoice);

            if (paid > 0)
            {
                var mode = PickMode(rng);
                var payment = new Payment
                {
                    MemberId = sub.MemberId,
                    BranchId = sub.BranchId,
                    Amount = paid,
                    Mode = mode,
                    Status = PaymentStatus.Captured,
                    PaidAtUtc = issuedOn.ToDateTime(new TimeOnly(11, 20)).AddMinutes(-330),
                    ReceivedBy = "Front desk",
                    IdempotencyKey = $"seed-pay-{sequence:D6}"
                };
                if (mode is PaymentMode.RazorpayLink or PaymentMode.Upi or PaymentMode.Card)
                {
                    payment.GatewayName = "razorpay";
                    payment.GatewayOrderId = $"order_SEED{sequence:D8}";
                    payment.GatewayPaymentId = $"pay_SEED{sequence:D8}";
                }
                if (mode == PaymentMode.Cheque) payment.ChequeNumber = $"{rng.Next(100000, 999999)}";
                payments.Add(payment);
                // Wired to the invoice by navigation so EF fills InvoiceId after insert.
                invoice.Payments.Add(payment);
            }
        }

        db.Invoices.AddRange(invoices);
        await db.SaveChangesAsync(ct);
        return;
    }

    private static InvoiceStatus ChooseInvoiceStatus(Subscription sub, DateOnly today, Random rng)
    {
        if (sub.Status is SubscriptionStatus.Expired or SubscriptionStatus.Cancelled)
            return rng.NextDouble() < 0.9 ? InvoiceStatus.Paid : InvoiceStatus.Overdue;

        var roll = rng.NextDouble();
        // ~8% of the live book is unpaid — enough to make the dues dashboard meaningful.
        if (roll < 0.86) return InvoiceStatus.Paid;
        if (roll < 0.92) return InvoiceStatus.PartiallyPaid;
        return sub.StartsOn.AddDays(5) < today ? InvoiceStatus.Overdue : InvoiceStatus.Issued;
    }

    private static PaymentMode PickMode(Random rng) => rng.NextDouble() switch
    {
        // UPI dominates in India; cash still real at the desk.
        < 0.46 => PaymentMode.Upi,
        < 0.66 => PaymentMode.RazorpayLink,
        < 0.80 => PaymentMode.Card,
        < 0.92 => PaymentMode.Cash,
        < 0.97 => PaymentMode.NetBanking,
        _ => PaymentMode.Cheque
    };

    /// <summary>Indian financial year runs April–March: FRG/26-27/000412.</summary>
    private static string InvoiceNumber(DateOnly issuedOn, int sequence)
    {
        var startYear = issuedOn.Month >= 4 ? issuedOn.Year : issuedOn.Year - 1;
        return $"FRG/{startYear % 100:D2}-{(startYear + 1) % 100:D2}/{sequence:D6}";
    }

    private static Plan PickPlan(List<Plan> plans, Random rng)
    {
        // Long-term plans dominate the book because of the steep annual discount.
        var roll = rng.NextDouble();
        var slug = roll switch
        {
            < 0.30 => "monthly",
            < 0.52 => "quarterly",
            < 0.70 => "half-yearly",
            < 0.90 => "annual-all-access",
            _ => "off-peak"
        };
        return plans.First(p => p.Slug == slug);
    }

    private static MemberStatus PickStatus(int daysSinceJoin, Random rng)
    {
        if (daysSinceJoin <= 7 && rng.NextDouble() < 0.55) return MemberStatus.Trial;
        var roll = rng.NextDouble();
        if (roll < 0.755) return MemberStatus.Active;
        if (roll < 0.815) return MemberStatus.Frozen;
        if (roll < 0.935) return MemberStatus.Expired;
        return MemberStatus.Cancelled;
    }

    /// <summary>Most members joined recently; a long tail goes back three years.</summary>
    private static int WeightedTenureDays(Random rng)
    {
        var roll = rng.NextDouble();
        return roll switch
        {
            < 0.06 => rng.Next(0, 8),
            < 0.30 => rng.Next(8, 95),
            < 0.60 => rng.Next(95, 250),
            < 0.85 => rng.Next(250, 550),
            _ => rng.Next(550, 1100)
        };
    }

    private static string PickWeighted((string Slug, double Weight)[] weights, Random rng)
    {
        var roll = rng.NextDouble();
        var cumulative = 0.0;
        foreach (var (slug, weight) in weights)
        {
            cumulative += weight;
            if (roll <= cumulative) return slug;
        }
        return weights[^1].Slug;
    }

    private static string UniqueEmail(string first, string last, HashSet<string> used)
    {
        var basePart = $"{first}.{last}".ToLowerInvariant().Replace("'", string.Empty);
        var candidate = $"{basePart}@example.com";
        var n = 1;
        while (!used.Add(candidate))
            candidate = $"{basePart}{++n}@example.com";
        return candidate;
    }

    /// <summary>Stable, opaque, URL-safe check-in token — deterministic so re-seeds match.</summary>
    private static string QrToken(int index)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"forge-member-qr-{index:D5}"));
        return Convert.ToHexString(bytes)[..32].ToLowerInvariant();
    }
}
