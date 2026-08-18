using System.Globalization;
using System.Text;
using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Admin;

/// <summary>
/// The member book: search and filter, the full profile, the activity timeline that makes a
/// desk conversation possible, bulk actions and CSV in/out. A member is always a
/// <see cref="Member"/> row paired with an identity, so anyone the desk creates can sign into
/// the portal the moment they are given a password.
/// </summary>
[ApiController]
[Route("api/admin/members")]
[Authorize(Roles = RoleNames.Admin)]
[Produces("application/json")]
public class AdminMembersController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IClock _clock;
    private readonly ILogger<AdminMembersController> _log;

    public AdminMembersController(
        GymDbContext db, UserManager<ApplicationUser> users, IClock clock, ILogger<AdminMembersController> log)
    {
        _db = db;
        _users = users;
        _clock = clock;
        _log = log;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<MemberListRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MemberListRow>>> List(
        [FromQuery] string? q,
        [FromQuery] int? branchId,
        [FromQuery] MemberStatus? status,
        [FromQuery] string? tag,
        [FromQuery] ChurnRiskBand? churn,
        [FromQuery] bool? expiringSoon,
        [FromQuery] bool? hasDues,
        [FromQuery] int? birthdayMonth,
        [FromQuery] string sort = "recent",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var today = _clock.Today;
        var query = _db.Members.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(m =>
                m.FullName.Contains(term) || m.Phone.Contains(term) ||
                m.MemberCode.Contains(term) || (m.Email != null && m.Email.Contains(term)));
        }
        if (branchId is not null) query = query.Where(m => m.HomeBranchId == branchId);
        if (status is not null) query = query.Where(m => m.Status == status);
        if (churn is not null) query = query.Where(m => m.ChurnRisk == churn);
        if (!string.IsNullOrWhiteSpace(tag)) query = query.Where(m => m.Tags != null && m.Tags.Contains(tag));
        if (birthdayMonth is >= 1 and <= 12)
            query = query.Where(m => m.DateOfBirth != null && m.DateOfBirth.Value.Month == birthdayMonth);

        if (expiringSoon == true)
        {
            var horizon = today.AddDays(7);
            query = query.Where(m => _db.Subscriptions.Any(s =>
                s.MemberId == m.Id && s.Status == SubscriptionStatus.Active
                && s.EndsOn >= today && s.EndsOn <= horizon));
        }
        if (hasDues == true)
        {
            query = query.Where(m => _db.Invoices.Any(i =>
                i.MemberId == m.Id && i.AmountDue > 0
                && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Refunded));
        }

        query = sort switch
        {
            "name" => query.OrderBy(m => m.FullName),
            "code" => query.OrderBy(m => m.MemberCode),
            "lastVisit" => query.OrderByDescending(m => m.LastVisitOn ?? DateOnly.MinValue),
            "churn" => query.OrderByDescending(m => m.ChurnScore),
            "joined" => query.OrderByDescending(m => m.JoinedOn),
            _ => query.OrderByDescending(m => m.Id)
        };

        var total = await query.CountAsync(ct);
        var size = Math.Clamp(pageSize, 1, 200);
        var pageNumber = Math.Max(1, page);

        var members = await query
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .Select(m => new
            {
                m.Id, m.MemberCode, m.FullName, m.Phone, m.Email, m.PhotoUrl,
                m.HomeBranchId, BranchName = m.HomeBranch.Name, m.Status, m.JoinedOn,
                m.LastVisitOn, m.CurrentStreakDays, m.ChurnRisk, m.Tags, m.DateOfBirth
            })
            .ToListAsync(ct);

        var ids = members.Select(m => m.Id).ToList();
        var rows = await BuildRowsAsync(members.Select(m => new MemberSeed(
            m.Id, m.MemberCode, m.FullName, m.Phone, m.Email, m.PhotoUrl, m.HomeBranchId,
            m.BranchName, m.Status, m.JoinedOn, m.LastVisitOn, m.CurrentStreakDays,
            m.ChurnRisk, m.Tags, m.DateOfBirth)).ToList(), ids, today, ct);

        return Ok(new PagedResult<MemberListRow>
        {
            Items = rows, Total = total, Page = pageNumber, PageSize = size
        });
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(MemberDetailResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MemberDetailResponse>> Detail(int id, CancellationToken ct)
    {
        var today = _clock.Today;

        var member = await _db.Members.AsNoTracking()
            .Include(m => m.HomeBranch)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
        if (member is null) return NotFound();

        var summary = (await BuildRowsAsync(
            new List<MemberSeed>
            {
                new(member.Id, member.MemberCode, member.FullName, member.Phone, member.Email,
                    member.PhotoUrl, member.HomeBranchId, member.HomeBranch.Name, member.Status,
                    member.JoinedOn, member.LastVisitOn, member.CurrentStreakDays, member.ChurnRisk,
                    member.Tags, member.DateOfBirth)
            },
            new List<int> { member.Id }, today, ct)).Single();

        var subscriptions = await _db.Subscriptions.AsNoTracking()
            .Where(s => s.MemberId == id)
            .OrderByDescending(s => s.StartsOn)
            .Select(s => new SubscriptionRow
            {
                Id = s.Id, MemberId = s.MemberId, MemberName = s.Member.FullName, MemberCode = s.Member.MemberCode,
                PlanId = s.PlanId, PlanName = s.Plan.Name, BranchName = s.Branch.Name, Status = s.Status,
                StartsOn = s.StartsOn.ToString("yyyy-MM-dd"), EndsOn = s.EndsOn.ToString("yyyy-MM-dd"),
                DaysLeft = s.EndsOn.DayNumber - today.DayNumber,
                PriceCharged = s.PriceCharged, DiscountAmount = s.DiscountAmount,
                ClassCreditsRemaining = s.ClassCreditsRemaining, PtCreditsRemaining = s.PtCreditsRemaining,
                FreezeStartsOn = s.FreezeStartsOn != null ? s.FreezeStartsOn.Value.ToString("yyyy-MM-dd") : null,
                FreezeEndsOn = s.FreezeEndsOn != null ? s.FreezeEndsOn.Value.ToString("yyyy-MM-dd") : null,
                FreezeDaysUsed = s.FreezeDaysUsed, FreezeDaysAllowed = s.Plan.FreezeDaysAllowed,
                AutoRenew = s.AutoRenew, CancellationReason = s.CancellationReason
            })
            .ToListAsync(ct);

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.MemberId == id)
            .OrderByDescending(i => i.IssuedOn).ThenByDescending(i => i.Id)
            .Take(50)
            .Select(i => new InvoiceRow
            {
                Id = i.Id, InvoiceNumber = i.InvoiceNumber, MemberId = i.MemberId,
                MemberName = i.Member.FullName, MemberCode = i.Member.MemberCode,
                BranchName = i.Branch.Name,
                IssuedOn = i.IssuedOn.ToString("yyyy-MM-dd"), DueOn = i.DueOn.ToString("yyyy-MM-dd"),
                Status = i.Status, GrandTotal = i.GrandTotal, AmountPaid = i.AmountPaid, AmountDue = i.AmountDue,
                RemindersSent = i.RemindersSent,
                DaysOverdue = i.AmountDue > 0 && i.DueOn < today ? today.DayNumber - i.DueOn.DayNumber : 0,
                PlanName = i.Subscription != null ? i.Subscription.Plan.Name : null
            })
            .ToListAsync(ct);

        var nowUtc = _clock.UtcNow;
        var upcoming = await _db.Bookings.AsNoTracking()
            .Where(b => b.MemberId == id && b.ClassSession.StartsAtUtc >= nowUtc)
            .Where(b => b.Status == BookingStatus.Booked || b.Status == BookingStatus.Waitlisted)
            .OrderBy(b => b.ClassSession.StartsAtUtc)
            .Take(10)
            .Select(b => new BookingRow
            {
                Id = b.Id,
                SessionId = b.ClassSessionId,
                FormatName = b.ClassSession.ClassFormat.Name,
                BranchName = b.ClassSession.Branch.Name,
                TrainerName = b.ClassSession.Trainer.FullName,
                Date = b.ClassSession.SessionDate.ToString("yyyy-MM-dd"),
                StartTime = b.ClassSession.StartTime.ToString("HH\\:mm"),
                Status = b.Status,
                WaitlistPosition = b.WaitlistPosition
            })
            .ToListAsync(ct);

        var stats = await BuildStatsAsync(id, today, ct);
        var timeline = await BuildTimelineAsync(id, ct);

        return Ok(new MemberDetailResponse
        {
            Summary = summary,
            Profile = new MemberProfile
            {
                Gender = member.Gender,
                DateOfBirth = member.DateOfBirth?.ToString("yyyy-MM-dd"),
                AddressLine = member.AddressLine,
                City = member.City,
                Pincode = member.Pincode,
                PrimaryGoal = member.PrimaryGoal,
                MedicalNotes = member.MedicalNotes,
                InjuryNotes = member.InjuryNotes,
                HeightCm = member.HeightCm,
                StartWeightKg = member.StartWeightKg,
                EmergencyContactName = member.EmergencyContactName,
                EmergencyContactPhone = member.EmergencyContactPhone,
                WaiverSigned = member.WaiverSigned,
                WaiverDocumentUrl = member.WaiverDocumentUrl,
                WaiverSignedAtUtc = member.WaiverSignedAtUtc,
                QrToken = member.QrToken,
                ReferralCode = member.ReferralCode,
                CorporateCode = member.CorporateCode,
                ConsentMarketing = member.ConsentMarketing,
                ConsentLeaderboard = member.ConsentLeaderboard,
                ConsentTransformationShowcase = member.ConsentTransformationShowcase
            },
            Subscriptions = subscriptions,
            Invoices = invoices,
            UpcomingBookings = upcoming,
            Timeline = timeline,
            Stats = stats
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(MemberListRow), StatusCodes.Status201Created)]
    public async Task<ActionResult<MemberListRow>> Create(UpsertMemberRequest request, CancellationToken ct)
    {
        var phone = NormalisePhone(request.Phone);
        if (phone.Length < 10)
        {
            ModelState.AddModelError(nameof(request.Phone), "Enter a 10-digit mobile number.");
            return ValidationProblem(ModelState);
        }

        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == request.HomeBranchId, ct);
        if (branch is null)
        {
            ModelState.AddModelError(nameof(request.HomeBranchId), "Pick a branch.");
            return ValidationProblem(ModelState);
        }

        if (await _db.Members.AnyAsync(m => m.Phone == phone, ct))
        {
            ModelState.AddModelError(nameof(request.Phone), "A member already uses that number.");
            return ValidationProblem(ModelState);
        }

        // A desk sign-up needs a login eventually; mint the identity now with a random
        // password so the member can be handed a reset rather than re-keyed later.
        var email = string.IsNullOrWhiteSpace(request.Email)
            ? $"{phone}@members.forge.local"
            : request.Email.Trim().ToLowerInvariant();

        if (await _users.FindByEmailAsync(email) is not null)
        {
            ModelState.AddModelError(nameof(request.Email), "That email already has an account.");
            return ValidationProblem(ModelState);
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            PhoneNumber = phone,
            FullName = request.FullName.Trim(),
            HomeBranchId = branch.Id,
            AvatarUrl = request.PhotoUrl,
            MustChangePassword = string.IsNullOrWhiteSpace(request.InitialPassword)
        };

        var password = string.IsNullOrWhiteSpace(request.InitialPassword)
            ? $"Forge@{Guid.NewGuid().ToString("N")[..10]}!"
            : request.InitialPassword;

        var created = await _users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors) ModelState.AddModelError(error.Code, error.Description);
            return ValidationProblem(ModelState);
        }
        await _users.AddToRoleAsync(user, RoleNames.Member);

        var member = new Member
        {
            UserId = user.Id,
            MemberCode = await NextMemberCodeAsync(branch, ct),
            HomeBranchId = branch.Id,
            QrToken = Guid.NewGuid().ToString("N"),
            ReferralCode = BuildReferralCode(request.FullName, user.Id),
            JoinedOn = _clock.Today,
            CreatedBy = User.Identity?.Name
        };
        Apply(member, request, phone, email);

        _db.Members.Add(member);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Member {Code} created at the desk by {User}", member.MemberCode, User.Identity?.Name);

        var row = (await BuildRowsAsync(
            new List<MemberSeed>
            {
                new(member.Id, member.MemberCode, member.FullName, member.Phone, member.Email,
                    member.PhotoUrl, member.HomeBranchId, branch.Name, member.Status, member.JoinedOn,
                    member.LastVisitOn, 0, member.ChurnRisk, member.Tags, member.DateOfBirth)
            },
            new List<int> { member.Id }, _clock.Today, ct)).Single();

        return CreatedAtAction(nameof(Detail), new { id = member.Id }, row);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(int id, UpsertMemberRequest request, CancellationToken ct)
    {
        var member = await _db.Members.Include(m => m.User).FirstOrDefaultAsync(m => m.Id == id, ct);
        if (member is null) return NotFound();

        var phone = NormalisePhone(request.Phone);
        if (phone.Length < 10)
        {
            ModelState.AddModelError(nameof(request.Phone), "Enter a 10-digit mobile number.");
            return ValidationProblem(ModelState);
        }
        if (await _db.Members.AnyAsync(m => m.Phone == phone && m.Id != id, ct))
        {
            ModelState.AddModelError(nameof(request.Phone), "Another member already uses that number.");
            return ValidationProblem(ModelState);
        }

        var email = string.IsNullOrWhiteSpace(request.Email) ? member.Email : request.Email.Trim().ToLowerInvariant();
        Apply(member, request, phone, email);
        member.UpdatedBy = User.Identity?.Name;

        // The login and the member row are one person; keep the contact details in step.
        if (member.User is { } user)
        {
            user.FullName = member.FullName;
            user.PhoneNumber = phone;
            user.HomeBranchId = member.HomeBranchId;
            user.AvatarUrl = member.PhotoUrl;
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetStatus(int id, [FromQuery] MemberStatus status, CancellationToken ct)
    {
        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (member is null) return NotFound();

        member.Status = status;
        member.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("bulk")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Bulk(BulkMemberRequest request, CancellationToken ct)
    {
        var members = await _db.Members.Where(m => request.MemberIds.Contains(m.Id)).ToListAsync(ct);
        if (members.Count == 0) return NotFound();

        foreach (var member in members)
        {
            switch (request.Action)
            {
                case "addTag" when !string.IsNullOrWhiteSpace(request.Tag):
                    var tags = SplitTags(member.Tags).ToList();
                    if (!tags.Contains(request.Tag, StringComparer.OrdinalIgnoreCase)) tags.Add(request.Tag.Trim());
                    member.Tags = string.Join(",", tags);
                    break;
                case "removeTag" when !string.IsNullOrWhiteSpace(request.Tag):
                    member.Tags = string.Join(",", SplitTags(member.Tags)
                        .Where(t => !string.Equals(t, request.Tag, StringComparison.OrdinalIgnoreCase)));
                    break;
                case "setStatus" when request.Status is { } status:
                    member.Status = status;
                    break;
                case "setBranch" when request.BranchId is { } branchId:
                    member.HomeBranchId = branchId;
                    break;
                default:
                    return Problem($"Unsupported bulk action \"{request.Action}\".", statusCode: 400);
            }
            member.UpdatedBy = User.Identity?.Name;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { updated = members.Count });
    }

    [HttpGet("export")]
    [Produces("text/csv")]
    public async Task<IActionResult> Export(
        [FromQuery] int? branchId, [FromQuery] MemberStatus? status, CancellationToken ct)
    {
        var today = _clock.Today;
        var rows = await _db.Members.AsNoTracking()
            .Where(m => (branchId == null || m.HomeBranchId == branchId) && (status == null || m.Status == status))
            .OrderBy(m => m.MemberCode)
            .Select(m => new
            {
                m.MemberCode, m.FullName, m.Phone, m.Email, BranchName = m.HomeBranch.Name,
                m.Status, m.JoinedOn, m.DateOfBirth, m.Gender, m.City, m.Tags, m.PrimaryGoal,
                m.LastVisitOn, m.CurrentStreakDays, m.ChurnRisk, m.WaiverSigned,
                Plan = _db.Subscriptions
                    .Where(s => s.MemberId == m.Id && s.Status == SubscriptionStatus.Active)
                    .OrderByDescending(s => s.EndsOn)
                    .Select(s => s.Plan.Name).FirstOrDefault(),
                EndsOn = _db.Subscriptions
                    .Where(s => s.MemberId == m.Id && s.Status == SubscriptionStatus.Active)
                    .OrderByDescending(s => s.EndsOn)
                    .Select(s => (DateOnly?)s.EndsOn).FirstOrDefault(),
                Dues = _db.Invoices
                    .Where(i => i.MemberId == m.Id && i.AmountDue > 0
                             && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Refunded)
                    .Sum(i => (decimal?)i.AmountDue) ?? 0m
            })
            .ToListAsync(ct);

        var csv = new StringBuilder();
        csv.AppendLine("MemberCode,FullName,Phone,Email,Branch,Status,JoinedOn,DateOfBirth,Gender,City," +
                       "Tags,Goal,Plan,MembershipEndsOn,DuesOutstanding,LastVisitOn,StreakDays,ChurnRisk,WaiverSigned");

        foreach (var r in rows)
        {
            csv.AppendLine(string.Join(",", new[]
            {
                Csv(r.MemberCode), Csv(r.FullName), Csv(r.Phone), Csv(r.Email), Csv(r.BranchName),
                Csv(r.Status.ToString()), Csv(r.JoinedOn.ToString("yyyy-MM-dd")),
                Csv(r.DateOfBirth?.ToString("yyyy-MM-dd")), Csv(r.Gender.ToString()), Csv(r.City),
                Csv(r.Tags), Csv(r.PrimaryGoal), Csv(r.Plan),
                Csv(r.EndsOn?.ToString("yyyy-MM-dd")), Csv(r.Dues.ToString("0.00", CultureInfo.InvariantCulture)),
                Csv(r.LastVisitOn?.ToString("yyyy-MM-dd")), Csv(r.CurrentStreakDays.ToString()),
                Csv(r.ChurnRisk.ToString()), Csv(r.WaiverSigned ? "Yes" : "No")
            }));
        }

        var fileName = $"forge-members-{today:yyyy-MM-dd}.csv";
        // The BOM is what makes Excel open a UTF-8 CSV with Indian names intact.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv", fileName);
    }

    /// <summary>
    /// Imports members from CSV with the same headers the export writes. Rows are validated
    /// individually: one bad row is reported and skipped rather than failing the whole file.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Import(
        IFormFile file, [FromForm] int defaultBranchId, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(nameof(file), "Choose a CSV file.");
            return ValidationProblem(ModelState);
        }

        var branches = await _db.Branches.ToDictionaryAsync(b => b.Name.ToLowerInvariant(), b => b, ct);
        var defaultBranch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == defaultBranchId, ct);
        if (defaultBranch is null)
        {
            ModelState.AddModelError(nameof(defaultBranchId), "Pick a branch for rows with no branch column.");
            return ValidationProblem(ModelState);
        }

        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var header = await reader.ReadLineAsync(ct);
        if (header is null) return Problem("The file is empty.", statusCode: 400);

        var columns = ParseCsvLine(header)
            .Select((name, index) => (name: name.Trim().ToLowerInvariant(), index))
            .ToDictionary(x => x.name, x => x.index);

        int Column(string name) => columns.TryGetValue(name.ToLowerInvariant(), out var index) ? index : -1;
        var nameCol = Column("fullname");
        var phoneCol = Column("phone");
        if (nameCol < 0 || phoneCol < 0)
            return Problem("The file needs at least FullName and Phone columns.", statusCode: 400);

        var imported = 0;
        var skipped = new List<string>();
        var lineNumber = 1;
        var existingPhones = (await _db.Members.Select(m => m.Phone).ToListAsync(ct)).ToHashSet();

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cells = ParseCsvLine(line);
            string Cell(int index) => index >= 0 && index < cells.Count ? cells[index].Trim() : string.Empty;

            var fullName = Cell(nameCol);
            var phone = NormalisePhone(Cell(phoneCol));

            if (string.IsNullOrWhiteSpace(fullName) || phone.Length < 10)
            {
                skipped.Add($"Line {lineNumber}: needs a name and a 10-digit number.");
                continue;
            }
            if (!existingPhones.Add(phone))
            {
                skipped.Add($"Line {lineNumber}: {phone} is already on the book.");
                continue;
            }

            var branchName = Cell(Column("branch")).ToLowerInvariant();
            var branch = branches.GetValueOrDefault(branchName) ?? defaultBranch;

            var emailCell = Cell(Column("email"));
            var email = string.IsNullOrWhiteSpace(emailCell) ? $"{phone}@members.forge.local" : emailCell.ToLowerInvariant();
            if (await _users.FindByEmailAsync(email) is not null)
            {
                skipped.Add($"Line {lineNumber}: {email} already has an account.");
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = email, Email = email, PhoneNumber = phone, FullName = fullName,
                HomeBranchId = branch.Id, MustChangePassword = true
            };
            var created = await _users.CreateAsync(user, $"Forge@{Guid.NewGuid().ToString("N")[..10]}!");
            if (!created.Succeeded)
            {
                skipped.Add($"Line {lineNumber}: {string.Join("; ", created.Errors.Select(e => e.Description))}");
                continue;
            }
            await _users.AddToRoleAsync(user, RoleNames.Member);

            var member = new Member
            {
                UserId = user.Id,
                MemberCode = await NextMemberCodeAsync(branch, ct),
                HomeBranchId = branch.Id,
                FullName = fullName,
                Phone = phone,
                Email = emailCell.Length > 0 ? emailCell : null,
                City = Cell(Column("city")).Length > 0 ? Cell(Column("city")) : null,
                Tags = Cell(Column("tags")).Length > 0 ? Cell(Column("tags")) : null,
                PrimaryGoal = Cell(Column("goal")).Length > 0 ? Cell(Column("goal")) : null,
                Status = MemberStatus.Lead,
                JoinedOn = DateOnly.TryParse(Cell(Column("joinedon")), out var joined) ? joined : _clock.Today,
                DateOfBirth = DateOnly.TryParse(Cell(Column("dateofbirth")), out var dob) ? dob : null,
                QrToken = Guid.NewGuid().ToString("N"),
                ReferralCode = BuildReferralCode(fullName, user.Id),
                CreatedBy = User.Identity?.Name
            };

            _db.Members.Add(member);
            await _db.SaveChangesAsync(ct);
            imported++;
        }

        _log.LogInformation("CSV import: {Imported} member(s) added, {Skipped} skipped", imported, skipped.Count);
        return Ok(new { imported, skipped = skipped.Take(50).ToList(), skippedCount = skipped.Count });
    }

    [HttpGet("birthdays")]
    [ProducesResponseType(typeof(IReadOnlyList<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Birthdays([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var today = _clock.Today;
        var span = Math.Clamp(days, 1, 90);

        var candidates = await _db.Members.AsNoTracking()
            .Where(m => m.DateOfBirth != null && m.Status != MemberStatus.Cancelled)
            .Select(m => new
            {
                m.Id, m.MemberCode, m.FullName, m.Phone, m.PhotoUrl,
                BranchName = m.HomeBranch.Name, m.DateOfBirth, m.Status
            })
            .ToListAsync(ct);

        // Day-of-year maths has to wrap the new year, so the window is computed in memory.
        var rows = candidates
            .Select(m =>
            {
                var dob = m.DateOfBirth!.Value;
                var next = new DateOnly(today.Year, dob.Month, Math.Min(dob.Day, DateTime.DaysInMonth(today.Year, dob.Month)));
                if (next < today) next = next.AddYears(1);
                return new
                {
                    m.Id, m.MemberCode, m.FullName, m.Phone, m.PhotoUrl, m.BranchName,
                    Date = next.ToString("yyyy-MM-dd"),
                    DaysAway = next.DayNumber - today.DayNumber,
                    TurningAge = next.Year - dob.Year,
                    Status = m.Status.ToString()
                };
            })
            .Where(m => m.DaysAway <= span)
            .OrderBy(m => m.DaysAway)
            .ToList();

        return Ok(rows);
    }

    // ------------------------------------------------------------------ helpers

    private record MemberSeed(
        int Id, string MemberCode, string FullName, string Phone, string? Email, string? PhotoUrl,
        int BranchId, string BranchName, MemberStatus Status, DateOnly JoinedOn, DateOnly? LastVisitOn,
        int StreakDays, ChurnRiskBand ChurnRisk, string? Tags, DateOnly? DateOfBirth);

    /// <summary>Decorates the base rows with the two facts every list shows: plan and dues.</summary>
    private async Task<List<MemberListRow>> BuildRowsAsync(
        List<MemberSeed> seeds, List<int> ids, DateOnly today, CancellationToken ct)
    {
        if (seeds.Count == 0) return new List<MemberListRow>();

        // Projected flat and reduced in memory: SQL Server cannot translate a GroupBy whose
        // selector reaches through a navigation, and this set is one page of members at most.
        var liveSubscriptions = await _db.Subscriptions.AsNoTracking()
            .Where(s => ids.Contains(s.MemberId))
            .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Frozen)
            .Select(s => new { s.MemberId, PlanName = s.Plan.Name, s.EndsOn })
            .ToListAsync(ct);

        var plans = liveSubscriptions
            .GroupBy(s => s.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.EndsOn).First());

        var dues = await _db.Invoices.AsNoTracking()
            .Where(i => ids.Contains(i.MemberId) && i.AmountDue > 0)
            .Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Refunded)
            .GroupBy(i => i.MemberId)
            .Select(g => new { MemberId = g.Key, Amount = g.Sum(i => i.AmountDue) })
            .ToDictionaryAsync(x => x.MemberId, x => x.Amount, ct);

        return seeds.Select(m =>
        {
            var plan = plans.GetValueOrDefault(m.Id);
            return new MemberListRow
            {
                Id = m.Id,
                MemberCode = m.MemberCode,
                FullName = m.FullName,
                Phone = m.Phone,
                Email = m.Email,
                PhotoUrl = m.PhotoUrl,
                BranchId = m.BranchId,
                BranchName = m.BranchName,
                Status = m.Status,
                JoinedOn = m.JoinedOn.ToString("yyyy-MM-dd"),
                PlanName = plan?.PlanName,
                MembershipEndsOn = plan?.EndsOn.ToString("yyyy-MM-dd"),
                DaysLeft = plan is null ? null : plan.EndsOn.DayNumber - today.DayNumber,
                DuesOutstanding = dues.GetValueOrDefault(m.Id),
                LastVisitOn = m.LastVisitOn?.ToString("yyyy-MM-dd"),
                CurrentStreakDays = m.StreakDays,
                ChurnRisk = m.ChurnRisk,
                Tags = SplitTags(m.Tags),
                DateOfBirth = m.DateOfBirth?.ToString("yyyy-MM-dd")
            };
        }).ToList();
    }

    private async Task<MemberStats> BuildStatsAsync(int memberId, DateOnly today, CancellationToken ct)
    {
        var since = today.AddDays(-30);
        return new MemberStats
        {
            TotalVisits = await _db.CheckIns.CountAsync(c => c.MemberId == memberId && !c.WasBlocked, ct),
            VisitsLast30Days = await _db.CheckIns.CountAsync(
                c => c.MemberId == memberId && !c.WasBlocked && c.VisitDate >= since, ct),
            ClassesAttended = await _db.Bookings.CountAsync(
                b => b.MemberId == memberId && b.Status == BookingStatus.Attended, ct),
            NoShows = await _db.Bookings.CountAsync(
                b => b.MemberId == memberId && b.Status == BookingStatus.NoShow, ct),
            LongestStreakDays = await _db.Members.Where(m => m.Id == memberId)
                .Select(m => m.LongestStreakDays).FirstAsync(ct),
            LifetimeValue = await _db.Payments
                .Where(p => p.MemberId == memberId && p.Status == PaymentStatus.Captured)
                .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m,
            ChurnScore = await _db.Members.Where(m => m.Id == memberId).Select(m => m.ChurnScore).FirstAsync(ct)
        };
    }

    /// <summary>
    /// One merged, reverse-chronological feed of everything that happened to this member:
    /// joins, payments, invoices, visits, bookings and freezes. This is the screen the desk
    /// actually reads before picking up the phone.
    /// </summary>
    private async Task<List<TimelineEntry>> BuildTimelineAsync(int memberId, CancellationToken ct)
    {
        var entries = new List<TimelineEntry>();

        var subs = await _db.Subscriptions.AsNoTracking()
            .Where(s => s.MemberId == memberId)
            .Select(s => new { PlanName = s.Plan.Name, s.StartsOn, s.EndsOn, s.Status, s.CreatedAtUtc, s.PriceCharged, s.CancelledOn, s.CancellationReason })
            .ToListAsync(ct);
        foreach (var s in subs)
        {
            entries.Add(new TimelineEntry
            {
                AtUtc = s.CreatedAtUtc,
                Kind = "membership",
                Title = $"{s.PlanName} started",
                Detail = $"{s.StartsOn:dd MMM yyyy} â†’ {s.EndsOn:dd MMM yyyy}",
                Amount = s.PriceCharged.ToString("C0", CultureInfo.GetCultureInfo("en-IN"))
            });
            if (s.CancelledOn is { } cancelled)
                entries.Add(new TimelineEntry
                {
                    AtUtc = cancelled.ToDateTime(TimeOnly.MinValue),
                    Kind = "cancellation",
                    Title = $"{s.PlanName} cancelled",
                    Detail = s.CancellationReason
                });
        }

        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.MemberId == memberId)
            .OrderByDescending(p => p.PaidAtUtc).Take(40)
            .Select(p => new { p.Amount, p.Mode, p.PaidAtUtc, p.Invoice.InvoiceNumber })
            .ToListAsync(ct);
        entries.AddRange(payments.Select(p => new TimelineEntry
        {
            AtUtc = p.PaidAtUtc,
            Kind = "payment",
            Title = $"Payment received â€” {p.Mode}",
            Detail = p.InvoiceNumber,
            Amount = p.Amount.ToString("C0", CultureInfo.GetCultureInfo("en-IN"))
        }));

        var visits = await _db.CheckIns.AsNoTracking()
            .Where(c => c.MemberId == memberId)
            .OrderByDescending(c => c.CheckInAtUtc).Take(30)
            .Select(c => new { c.CheckInAtUtc, c.CheckOutAtUtc, BranchName = c.Branch.Name, c.Source, c.WasBlocked, c.BlockReason })
            .ToListAsync(ct);
        entries.AddRange(visits.Select(v => new TimelineEntry
        {
            AtUtc = v.CheckInAtUtc,
            Kind = v.WasBlocked ? "blocked" : "visit",
            Title = v.WasBlocked ? "Entry refused" : $"Checked in at {v.BranchName}",
            Detail = v.WasBlocked
                ? v.BlockReason
                : v.CheckOutAtUtc is null
                    ? $"{v.Source} Â· still on the floor"
                    : $"{v.Source} Â· {(int)(v.CheckOutAtUtc.Value - v.CheckInAtUtc).TotalMinutes} min"
        }));

        var bookings = await _db.Bookings.AsNoTracking()
            .Where(b => b.MemberId == memberId)
            .OrderByDescending(b => b.BookedAtUtc).Take(30)
            .Select(b => new
            {
                b.BookedAtUtc, b.Status, FormatName = b.ClassSession.ClassFormat.Name,
                b.ClassSession.SessionDate, b.ClassSession.StartTime, TrainerName = b.ClassSession.Trainer.FullName
            })
            .ToListAsync(ct);
        entries.AddRange(bookings.Select(b => new TimelineEntry
        {
            AtUtc = b.BookedAtUtc,
            Kind = "booking",
            Title = $"{b.FormatName} â€” {b.Status}",
            Detail = $"{b.SessionDate:ddd dd MMM} at {b.StartTime:HH\\:mm} with {b.TrainerName}"
        }));

        return entries.OrderByDescending(e => e.AtUtc).Take(80).ToList();
    }

    private void Apply(Member member, UpsertMemberRequest r, string phone, string? email)
    {
        member.FullName = r.FullName.Trim();
        member.Phone = phone;
        member.Email = email;
        member.HomeBranchId = r.HomeBranchId;
        member.DateOfBirth = r.DateOfBirth;
        member.Gender = r.Gender;
        member.PhotoUrl = r.PhotoUrl;
        member.AddressLine = r.AddressLine;
        member.City = r.City;
        member.Pincode = r.Pincode;
        member.Status = r.Status;
        member.PrimaryGoal = r.PrimaryGoal;
        member.MedicalNotes = r.MedicalNotes;
        member.InjuryNotes = r.InjuryNotes;
        member.HeightCm = r.HeightCm;
        member.StartWeightKg = r.StartWeightKg;
        member.EmergencyContactName = r.EmergencyContactName;
        member.EmergencyContactPhone = r.EmergencyContactPhone;
        member.WaiverDocumentUrl = r.WaiverDocumentUrl;
        member.Tags = r.Tags;
        member.CorporateCode = r.CorporateCode;
        member.ConsentMarketing = r.ConsentMarketing;
        member.ConsentLeaderboard = r.ConsentLeaderboard;
        member.ConsentTransformationShowcase = r.ConsentTransformationShowcase;

        // The signature timestamp is evidence; only stamp it on the transition.
        if (r.WaiverSigned && !member.WaiverSigned)
        {
            member.WaiverSigned = true;
            member.WaiverSignedAtUtc = _clock.UtcNow;
        }
        else if (!r.WaiverSigned)
        {
            member.WaiverSigned = false;
            member.WaiverSignedAtUtc = null;
        }
    }

    private async Task<string> NextMemberCodeAsync(Branch branch, CancellationToken ct)
    {
        var prefix = $"FRG-{branch.MemberCodePrefix}-";
        var highest = await _db.Members
            .Where(m => m.MemberCode.StartsWith(prefix))
            .Select(m => m.MemberCode)
            .OrderByDescending(code => code)
            .FirstOrDefaultAsync(ct);

        var next = 1;
        if (highest is not null && int.TryParse(highest[prefix.Length..], out var parsed)) next = parsed + 1;
        return $"{prefix}{next:D5}";
    }

    private static string BuildReferralCode(string fullName, int userId)
    {
        var initials = new string(fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]))
            .Take(3)
            .ToArray());
        return $"{(initials.Length == 0 ? "FRG" : initials)}{1000 + userId}";
    }

    internal static string NormalisePhone(string raw)
    {
        var digits = new string((raw ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length > 10 && digits.StartsWith("91")) digits = digits[^10..];
        if (digits.Length > 10 && digits.StartsWith('0')) digits = digits[^10..];
        return digits;
    }

    internal static IReadOnlyList<string> SplitTags(string? tags) =>
        string.IsNullOrWhiteSpace(tags)
            ? Array.Empty<string>()
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    /// <summary>Minimal RFC-4180 reader â€” quoted cells with embedded commas are the norm in exports.</summary>
    private static List<string> ParseCsvLine(string line)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else current.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { cells.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        cells.Add(current.ToString());
        return cells;
    }
}
