namespace Gym.Core.Entities;

/// <summary>
/// v1 ships Admin and Member. BranchManager, Trainer and FrontDesk are declared now and
/// created by the seeder so Phase-3 role work is a policy change, not a migration.
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Member = "Member";
    public const string BranchManager = "BranchManager";
    public const string Trainer = "Trainer";
    public const string FrontDesk = "FrontDesk";

    public static readonly (string Name, string Description)[] All =
    {
        (Admin, "Owner. Full access to every branch, every module and all public-site content."),
        (Member, "Customer. Own data only: bookings, workouts, progress, invoices."),
        (BranchManager, "Phase 3 — operations scoped to assigned branches."),
        (Trainer, "Phase 3 — own classes, assigned clients, program authoring."),
        (FrontDesk, "Phase 3 — check-ins, POS and lead capture at one branch.")
    };
}
