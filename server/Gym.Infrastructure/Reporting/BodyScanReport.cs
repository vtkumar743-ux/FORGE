using Gym.Core.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Gym.Infrastructure.Reporting;

/// <summary>
/// Everything the report renders, resolved before the document is composed. QuestPDF's
/// composition runs synchronously, so no query may hide inside it.
/// </summary>
public record BodyScanReportModel
{
    public required string MemberName { get; init; }
    public required string MemberCode { get; init; }
    public required string BranchName { get; init; }
    public required string GeneratedOn { get; init; }
    public required IReadOnlyList<BodyScan> Scans { get; init; }
    public decimal? HeightCm { get; init; }
    public string? Goal { get; init; }
    public int CheckInsInPeriod { get; init; }
    public int WorkoutsInPeriod { get; init; }
    public int PersonalRecords { get; init; }
    public int CurrentStreakDays { get; init; }
    public IReadOnlyList<StrengthRow> Strength { get; init; } = Array.Empty<StrengthRow>();
    public string? CoachNote { get; init; }
}

public record StrengthRow(string Exercise, decimal FirstE1Rm, decimal LatestE1Rm, DateOnly LatestOn);

/// <summary>
/// The shareable progress report (Module 4.4). Printed on the same palette as the site —
/// black, bone, gold — because a member forwards this to their family, and a generic
/// blue-and-white PDF undoes the impression the rest of the product just made.
/// </summary>
public class BodyScanReport : IDocument
{
    private const string Ink = "#0A0A0A";
    private const string Bone = "#F4F1EA";
    private const string Gold = "#ECD06F";
    private const string Muted = "#8A8578";
    private const string Line = "#2A2A28";

    private readonly BodyScanReportModel _model;

    public BodyScanReport(BodyScanReportModel model) => _model = model;

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"FORGE progress report · {_model.MemberName}",
        Author = "FORGE",
        Subject = "Body composition and training progress"
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.DefaultTextStyle(t => t.FontFamily(Fonts.Calibri).FontSize(10).FontColor(Ink));
            page.Content().Column(col =>
            {
                col.Item().Element(Masthead);
                col.Item().PaddingHorizontal(36).PaddingTop(24).Element(Headline);
                col.Item().PaddingHorizontal(36).PaddingTop(18).Element(Snapshot);
                col.Item().PaddingHorizontal(36).PaddingTop(22).Element(TrendTable);
                if (_model.Strength.Count > 0)
                    col.Item().PaddingHorizontal(36).PaddingTop(22).Element(StrengthTable);
                col.Item().PaddingHorizontal(36).PaddingTop(22).Element(TrainingBlock);
                if (!string.IsNullOrWhiteSpace(_model.CoachNote))
                    col.Item().PaddingHorizontal(36).PaddingTop(18).Element(CoachNoteBlock);
                col.Item().PaddingTop(24).Element(Footer);
            });
        });
    }

    private void Masthead(IContainer container) =>
        container.Background(Ink).PaddingVertical(26).PaddingHorizontal(36).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("FORGE").FontSize(26).Bold().LetterSpacing(0.24f).FontColor(Bone);
                col.Item().PaddingTop(2).Text("Progress report").FontSize(9).LetterSpacing(0.18f).FontColor(Gold);
            });
            row.ConstantItem(190).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text(_model.MemberName).FontSize(13).SemiBold().FontColor(Bone);
                col.Item().AlignRight().PaddingTop(1).Text($"{_model.MemberCode} · {_model.BranchName}")
                    .FontSize(8.5f).FontColor(Muted);
                col.Item().AlignRight().PaddingTop(1).Text($"Issued {_model.GeneratedOn}").FontSize(8.5f).FontColor(Muted);
            });
        });

    private void Headline(IContainer container)
    {
        var first = _model.Scans.FirstOrDefault();
        var latest = _model.Scans.LastOrDefault();

        container.Column(col =>
        {
            if (first is null || latest is null || _model.Scans.Count < 2)
            {
                col.Item().Text("Your first measurement").FontSize(19).SemiBold();
                col.Item().PaddingTop(4).Text(
                        "This is the baseline everything after it is measured against. Book the next scan in four to six weeks.")
                    .FontSize(10).FontColor(Muted);
                return;
            }

            var weeks = Math.Max(1, (latest.ScanDate.DayNumber - first.ScanDate.DayNumber) / 7);
            var weightDelta = latest.WeightKg - first.WeightKg;
            var fatDelta = latest.FatMassKg is { } lf && first.FatMassKg is { } ff ? lf - ff : (decimal?)null;
            var muscleDelta = latest.SkeletalMuscleMassKg is { } lm && first.SkeletalMuscleMassKg is { } fm ? lm - fm : (decimal?)null;

            col.Item().Text($"{weeks} weeks, measured").FontSize(19).SemiBold();

            var sentence = fatDelta is not null && muscleDelta is not null
                ? $"Body weight {Signed(weightDelta)} kg — fat mass {Signed(fatDelta.Value)} kg and skeletal muscle {Signed(muscleDelta.Value)} kg. "
                  + "The scale on its own would have told you a different story."
                : $"Body weight {Signed(weightDelta)} kg across {_model.Scans.Count} measurements.";

            col.Item().PaddingTop(4).Text(sentence).FontSize(10).FontColor(Muted);
        });
    }

    private void Snapshot(IContainer container)
    {
        var latest = _model.Scans.LastOrDefault();
        container.Row(row =>
        {
            void Tile(string label, string value, string? sub)
            {
                row.RelativeItem().PaddingRight(8).Border(1).BorderColor("#E2DED2").Padding(12).Column(col =>
                {
                    col.Item().Text(label.ToUpperInvariant()).FontSize(7.5f).LetterSpacing(0.14f).FontColor(Muted);
                    col.Item().PaddingTop(5).Text(value).FontSize(18).SemiBold();
                    if (sub is not null) col.Item().PaddingTop(2).Text(sub).FontSize(8).FontColor(Muted);
                });
            }

            Tile("Weight", latest is null ? "—" : $"{latest.WeightKg:0.#} kg",
                _model.HeightCm is { } h && latest is not null
                    ? $"BMI {latest.WeightKg / (h / 100m * (h / 100m)):0.#}"
                    : null);
            Tile("Body fat", latest?.BodyFatPercent is { } bf ? $"{bf:0.#}%" : "—",
                latest?.FatMassKg is { } fm ? $"{fm:0.#} kg fat mass" : null);
            Tile("Muscle", latest?.SkeletalMuscleMassKg is { } sm ? $"{sm:0.#} kg" : "—", "Skeletal muscle");
            Tile("Streak", $"{_model.CurrentStreakDays}", _model.CurrentStreakDays == 1 ? "day" : "days");
        });
    }

    private void TrendTable(IContainer container) =>
        container.Column(col =>
        {
            col.Item().Element(SectionTitle("Composition trend"));
            col.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.5f);
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                });

                foreach (var head in new[] { "Date", "Weight", "Body fat", "Fat mass", "Muscle", "Visceral" })
                    table.Cell().Element(HeadCell).Text(head);

                // Newest first: the number someone opens this to see is the last one measured.
                foreach (var scan in _model.Scans.OrderByDescending(s => s.ScanDate).Take(8))
                {
                    table.Cell().Element(BodyCell).Text(scan.ScanDate.ToString("dd MMM yyyy"));
                    table.Cell().Element(BodyCell).Text($"{scan.WeightKg:0.#} kg");
                    table.Cell().Element(BodyCell).Text(scan.BodyFatPercent is { } bfp ? $"{bfp:0.#}%" : "—");
                    table.Cell().Element(BodyCell).Text(scan.FatMassKg is { } fmk ? $"{fmk:0.#} kg" : "—");
                    table.Cell().Element(BodyCell).Text(scan.SkeletalMuscleMassKg is { } smm ? $"{smm:0.#} kg" : "—");
                    table.Cell().Element(BodyCell).Text(scan.VisceralFatLevel is { } vfl ? $"{vfl:0.#}" : "—");
                }
            });
        });

    private void StrengthTable(IContainer container) =>
        container.Column(col =>
        {
            col.Item().Element(SectionTitle("Strength"));
            col.Item().PaddingTop(2)
                .Text("Estimated one-rep max, so a heavier set of eight counts as the progress it is.")
                .FontSize(8.5f).FontColor(Muted);
            col.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2);
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn(1.2f);
                });

                foreach (var head in new[] { "Lift", "First", "Latest", "Change", "Last done" })
                    table.Cell().Element(HeadCell).Text(head);

                foreach (var row in _model.Strength)
                {
                    var delta = row.LatestE1Rm - row.FirstE1Rm;
                    table.Cell().Element(BodyCell).Text(row.Exercise);
                    table.Cell().Element(BodyCell).Text($"{row.FirstE1Rm:0.#} kg");
                    table.Cell().Element(BodyCell).Text($"{row.LatestE1Rm:0.#} kg");
                    table.Cell().Element(BodyCell).Text(text =>
                        text.Span($"{Signed(delta)} kg").FontColor(delta >= 0 ? "#2F6F4F" : "#8A3A2E").SemiBold());
                    table.Cell().Element(BodyCell).Text(row.LatestOn.ToString("dd MMM"));
                }
            });
        });

    private void TrainingBlock(IContainer container) =>
        container.Column(col =>
        {
            col.Item().Element(SectionTitle("Training in this period"));
            col.Item().PaddingTop(8).Row(row =>
            {
                void Stat(string value, string label)
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(value).FontSize(20).SemiBold();
                        c.Item().PaddingTop(1).Text(label).FontSize(8.5f).FontColor(Muted);
                    });
                }

                Stat(_model.CheckInsInPeriod.ToString(), "Visits");
                Stat(_model.WorkoutsInPeriod.ToString(), "Sessions logged");
                Stat(_model.PersonalRecords.ToString(), "Personal records");
                Stat(_model.Goal ?? "General fitness", "Goal on file");
            });
        });

    private void CoachNoteBlock(IContainer container) =>
        container.Background("#FAF8F2").BorderLeft(3).BorderColor(Gold).Padding(14).Column(col =>
        {
            col.Item().Text("From your coach").FontSize(8).LetterSpacing(0.14f).FontColor(Muted);
            col.Item().PaddingTop(4).Text(_model.CoachNote!).FontSize(10);
        });

    private void Footer(IContainer container) =>
        container.Background(Ink).PaddingVertical(16).PaddingHorizontal(36).Row(row =>
        {
            row.RelativeItem().Text(
                    "Measurements are taken on the branch InBody unit or entered by the member; "
                    + "readings vary with hydration and time of day. Not a medical assessment.")
                .FontSize(7.5f).FontColor(Muted);
            row.ConstantItem(120).AlignRight().Text("forge.fit").FontSize(9).FontColor(Gold);
        });

    private static Func<IContainer, IContainer> SectionTitle(string title) =>
        container =>
        {
            container.BorderBottom(1).BorderColor("#E2DED2").PaddingBottom(6)
                .Text(title.ToUpperInvariant()).FontSize(8.5f).LetterSpacing(0.16f).SemiBold();
            return container;
        };

    private static IContainer HeadCell(IContainer container) =>
        container.BorderBottom(1).BorderColor(Line).PaddingVertical(5).DefaultTextStyle(
            t => t.FontSize(7.5f).LetterSpacing(0.1f).FontColor(Muted));

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(1).BorderColor("#EFEBE0").PaddingVertical(6).DefaultTextStyle(t => t.FontSize(9.5f));

    private static string Signed(decimal value) => value >= 0 ? $"+{value:0.#}" : $"{value:0.#}";
}
