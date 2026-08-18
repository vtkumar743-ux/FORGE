using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "auth");

            migrationBuilder.EnsureSchema(
                name: "gym");

            migrationBuilder.CreateTable(
                name: "Badges",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IconKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Tier = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CriteriaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Badges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlogPosts",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Excerpt = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BodyJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    AuthorRole = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ReadMinutes = table.Column<int>(type: "int", nullable: false),
                    SeoTitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SeoDescription = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    OgImageUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    ViewCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Branches",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    MemberCodePrefix = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    City = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    AddressLine1 = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    AddressLine2 = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    State = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Pincode = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    WhatsAppNumber = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    GoogleMapsUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    GooglePlaceId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    WeekdayOpen = table.Column<TimeOnly>(type: "time", nullable: false),
                    WeekdayClose = table.Column<TimeOnly>(type: "time", nullable: false),
                    WeekendOpen = table.Column<TimeOnly>(type: "time", nullable: false),
                    WeekendClose = table.Column<TimeOnly>(type: "time", nullable: false),
                    FloorAreaSqft = table.Column<int>(type: "int", nullable: false),
                    OccupancyCapacity = table.Column<int>(type: "int", nullable: false),
                    Gstin = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    HeroImageUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    TourVideoUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ShortPitch = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClassFormats",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    DefaultCapacity = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Intensity = table.Column<int>(type: "int", nullable: false),
                    EstimatedCalories = table.Column<int>(type: "int", nullable: false),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IconKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ShowOnWebsite = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassFormats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CmsPages",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SeoTitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SeoDescription = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SeoKeywords = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    OgImageUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CanonicalUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    NoIndex = table.Column<bool>(type: "bit", nullable: false),
                    StructuredDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsSystemPage = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CmsPages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Coupons",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscountType = table.Column<int>(type: "int", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    MaxDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MinOrderAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false),
                    UsageCap = table.Column<int>(type: "int", nullable: true),
                    UsageCount = table.Column<int>(type: "int", nullable: false),
                    PerMemberCap = table.Column<int>(type: "int", nullable: true),
                    BranchScope = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PlanScope = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ShowAsWebsiteBanner = table.Column<bool>(type: "bit", nullable: false),
                    BannerHeadline = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coupons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Exercises",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    PrimaryMuscle = table.Column<int>(type: "int", nullable: false),
                    SecondaryMuscles = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Equipment = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    VideoUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Cues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsStrengthTracked = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exercises", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaAssets",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    OriginalUrl = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    VariantsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PosterUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    BlurDataUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AltText = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Credit = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SourceUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Folder = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UploadedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plans",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Tagline = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Cycle = table.Column<int>(type: "int", nullable: false),
                    AccessScope = table.Column<int>(type: "int", nullable: false),
                    DurationDays = table.Column<int>(type: "int", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdmissionFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GstRatePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    SacCode = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    ClassCredits = table.Column<int>(type: "int", nullable: true),
                    PtSessionCredits = table.Column<int>(type: "int", nullable: true),
                    GuestPasses = table.Column<int>(type: "int", nullable: true),
                    FreezeDaysAllowed = table.Column<int>(type: "int", nullable: false),
                    FreezeFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AccessWindowStart = table.Column<TimeOnly>(type: "time", nullable: true),
                    AccessWindowEnd = table.Column<TimeOnly>(type: "time", nullable: true),
                    FeaturesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TrustMicrocopy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsMostPopular = table.Column<bool>(type: "bit", nullable: false),
                    ShowOnWebsite = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SellPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GstRatePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    HsnCode = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    LowStockThreshold = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteSettings",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Group = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    HelpText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Challenges",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Metric = table.Column<int>(type: "int", nullable: false),
                    TargetValue = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RewardDescription = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Challenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Challenges_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FaqItems",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Question = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaqItems_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rooms_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Testimonials",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    AuthorRole = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    AuthorPhotoUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Quote = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    Program = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    GoogleReviewUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Testimonials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Testimonials_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    HomeBranchId = table.Column<int>(type: "int", nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Branches_HomeBranchId",
                        column: x => x.HomeBranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CmsSections",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CmsPageId = table.Column<int>(type: "int", nullable: false),
                    SectionType = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AdminLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    ContentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DraftContentJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CmsSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CmsSections_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CmsSections_CmsPages_CmsPageId",
                        column: x => x.CmsPageId,
                        principalSchema: "gym",
                        principalTable: "CmsPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanBranchPrices",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdmissionFee = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanBranchPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanBranchPrices_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanBranchPrices_Plans_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "gym",
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BranchStocks",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    QuantityOnHand = table.Column<int>(type: "int", nullable: false),
                    QuantityReserved = table.Column<int>(type: "int", nullable: false),
                    LastRestockedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShelfLocation = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchStocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchStocks_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BranchStocks_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "gym",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockTransfers",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    FromBranchId = table.Column<int>(type: "int", nullable: false),
                    ToBranchId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ReceivedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Branches_FromBranchId",
                        column: x => x.FromBranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Branches_ToBranchId",
                        column: x => x.ToBranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "gym",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "auth",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                schema: "auth",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                schema: "auth",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "auth",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                schema: "auth",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Members",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    MemberCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    HomeBranchId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    PhotoUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    AddressLine = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    City = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Pincode = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    JoinedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    PrimaryGoal = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    MedicalNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InjuryNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HeightCm = table.Column<decimal>(type: "decimal(5,1)", nullable: true),
                    StartWeightKg = table.Column<decimal>(type: "decimal(5,1)", nullable: true),
                    EmergencyContactName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    WaiverSigned = table.Column<bool>(type: "bit", nullable: false),
                    WaiverSignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WaiverDocumentUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    QrToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReferralCode = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    ReferredByMemberId = table.Column<int>(type: "int", nullable: true),
                    CorporateCode = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ConsentMarketing = table.Column<bool>(type: "bit", nullable: false),
                    ConsentLeaderboard = table.Column<bool>(type: "bit", nullable: false),
                    ConsentTransformationShowcase = table.Column<bool>(type: "bit", nullable: false),
                    CurrentStreakDays = table.Column<int>(type: "int", nullable: false),
                    LongestStreakDays = table.Column<int>(type: "int", nullable: false),
                    LastVisitOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ChurnRisk = table.Column<int>(type: "int", nullable: false),
                    ChurnScore = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Members_Branches_HomeBranchId",
                        column: x => x.HomeBranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Members_Members_ReferredByMemberId",
                        column: x => x.ReferredByMemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Members_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(88)", maxLength: 88, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByIp = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByIp = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ReasonRevoked = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Trainers",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    PortraitUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DemoVideoUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Headline = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Bio = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Specialties = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Certifications = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    YearsExperience = table.Column<int>(type: "int", nullable: false),
                    InstagramUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PrimaryBranchId = table.Column<int>(type: "int", nullable: false),
                    PerClassRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PerPtSessionRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CommissionPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    PtSessionPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AverageRating = table.Column<decimal>(type: "decimal(3,2)", nullable: false),
                    RatingCount = table.Column<int>(type: "int", nullable: false),
                    AcceptsPtClients = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ShowOnWebsite = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trainers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trainers_Branches_PrimaryBranchId",
                        column: x => x.PrimaryBranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trainers_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BodyScans",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    ScanDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    BodyFatPercent = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    SkeletalMuscleMassKg = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    FatMassKg = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    VisceralFatLevel = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    Bmi = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    BasalMetabolicRate = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    TotalBodyWaterL = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    InBodyScore = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    ChestCm = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    WaistCm = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    HipCm = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    ThighCm = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    ArmCm = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    MeasuredBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DeviceName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BodyScans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BodyScans_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeedPosts",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    MetaJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LikeCount = table.Column<int>(type: "int", nullable: false),
                    CommentCount = table.Column<int>(type: "int", nullable: false),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedPosts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FeedPosts_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Leads",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    SourceDetail = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Goal = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreferredTime = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    TrialRequestedFor = table.Column<DateOnly>(type: "date", nullable: true),
                    InterestedPlanId = table.Column<int>(type: "int", nullable: true),
                    AssignedTo = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    FirstResponseAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextFollowUpAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConvertedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ConvertedMemberId = table.Column<int>(type: "int", nullable: true),
                    LostReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SequenceActive = table.Column<bool>(type: "bit", nullable: false),
                    SequenceStep = table.Column<int>(type: "int", nullable: false),
                    UtmSource = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UtmCampaign = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leads_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Leads_Members_ConvertedMemberId",
                        column: x => x.ConvertedMemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Leads_Plans_InterestedPlanId",
                        column: x => x.InterestedPlanId,
                        principalSchema: "gym",
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MemberBadges",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    BadgeId = table.Column<int>(type: "int", nullable: false),
                    AwardedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Context = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsSeen = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberBadges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberBadges_Badges_BadgeId",
                        column: x => x.BadgeId,
                        principalSchema: "gym",
                        principalTable: "Badges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberBadges_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    TemplateKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveryFailed = table.Column<bool>(type: "bit", nullable: false),
                    DeliveryError = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: true),
                    PlacedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ChargedToMemberTab = table.Column<bool>(type: "bit", nullable: false),
                    SoldBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProgressPhotos",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    TakenOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Pose = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(5,1)", nullable: true),
                    IsPrivate = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgressPhotos_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CancelledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PriceCharged = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CouponId = table.Column<int>(type: "int", nullable: true),
                    ClassCreditsRemaining = table.Column<int>(type: "int", nullable: false),
                    PtCreditsRemaining = table.Column<int>(type: "int", nullable: false),
                    FreezeStartsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    FreezeEndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    FreezeDaysUsed = table.Column<int>(type: "int", nullable: false),
                    AutoRenew = table.Column<bool>(type: "bit", nullable: false),
                    GatewayMandateRef = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    NextBillingOn = table.Column<DateOnly>(type: "date", nullable: true),
                    UpgradedFromSubscriptionId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Coupons_CouponId",
                        column: x => x.CouponId,
                        principalSchema: "gym",
                        principalTable: "Coupons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Plans_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "gym",
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Subscriptions_UpgradedFromSubscriptionId",
                        column: x => x.UpgradedFromSubscriptionId,
                        principalSchema: "gym",
                        principalTable: "Subscriptions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Transformations",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberDisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: true),
                    BeforeImageUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    AfterImageUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DurationWeeks = table.Column<int>(type: "int", nullable: false),
                    Program = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    TrainerName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    WeightBeforeKg = table.Column<decimal>(type: "decimal(5,1)", nullable: true),
                    WeightAfterKg = table.Column<decimal>(type: "decimal(5,1)", nullable: true),
                    Story = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    ConsentGiven = table.Column<bool>(type: "bit", nullable: false),
                    ConsentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transformations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transformations_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Transformations_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ClassSchedules",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ClassFormatId = table.Column<int>(type: "int", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: true),
                    TrainerId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    BookingOpensHoursBefore = table.Column<int>(type: "int", nullable: false),
                    CancelCutoffHoursBefore = table.Column<int>(type: "int", nullable: false),
                    WaitlistEnabled = table.Column<bool>(type: "bit", nullable: false),
                    WaitlistCapacity = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassSchedules_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassSchedules_ClassFormats_ClassFormatId",
                        column: x => x.ClassFormatId,
                        principalSchema: "gym",
                        principalTable: "ClassFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassSchedules_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "gym",
                        principalTable: "Rooms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClassSchedules_Trainers_TrainerId",
                        column: x => x.TrainerId,
                        principalSchema: "gym",
                        principalTable: "Trainers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DietPlans",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: true),
                    TrainerId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Author = table.Column<int>(type: "int", nullable: false),
                    GenerationContextJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TargetCalories = table.Column<int>(type: "int", nullable: false),
                    ProteinGrams = table.Column<int>(type: "int", nullable: false),
                    CarbGrams = table.Column<int>(type: "int", nullable: false),
                    FatGrams = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsVegetarian = table.Column<bool>(type: "bit", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DietPlans_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DietPlans_Trainers_TrainerId",
                        column: x => x.TrainerId,
                        principalSchema: "gym",
                        principalTable: "Trainers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WorkoutPrograms",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MemberId = table.Column<int>(type: "int", nullable: true),
                    TrainerId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Author = table.Column<int>(type: "int", nullable: false),
                    GenerationContextJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationWeeks = table.Column<int>(type: "int", nullable: false),
                    DaysPerWeek = table.Column<int>(type: "int", nullable: false),
                    Goal = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    IsTemplate = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutPrograms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkoutPrograms_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkoutPrograms_Trainers_TrainerId",
                        column: x => x.TrainerId,
                        principalSchema: "gym",
                        principalTable: "Trainers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LeadFollowUps",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeadId = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Owner = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsAutomated = table.Column<bool>(type: "bit", nullable: false),
                    StageAtCreation = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadFollowUps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadFollowUps_Leads_LeadId",
                        column: x => x.LeadId,
                        principalSchema: "gym",
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Referrals",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferrerMemberId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    InviteeName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    InviteePhone = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    InviteeMemberId = table.Column<int>(type: "int", nullable: true),
                    LeadId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RewardAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReferrerRewarded = table.Column<bool>(type: "bit", nullable: false),
                    InviteeRewarded = table.Column<bool>(type: "bit", nullable: false),
                    ConvertedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Referrals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Referrals_Leads_LeadId",
                        column: x => x.LeadId,
                        principalSchema: "gym",
                        principalTable: "Leads",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Referrals_Members_InviteeMemberId",
                        column: x => x.InviteeMemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Referrals_Members_ReferrerMemberId",
                        column: x => x.ReferrerMemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderLines",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GstRatePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderLines_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "gym",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "gym",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    SubscriptionId = table.Column<int>(type: "int", nullable: true),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxableValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CgstAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SgstAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IgstAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RoundOff = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountDue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SupplierGstin = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PlaceOfSupply = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CustomerGstin = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PdfUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RemindersSent = table.Column<int>(type: "int", nullable: false),
                    LastReminderAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "gym",
                        principalTable: "Orders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Invoices_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalSchema: "gym",
                        principalTable: "Subscriptions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClassSessions",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassScheduleId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ClassFormatId = table.Column<int>(type: "int", nullable: false),
                    TrainerId = table.Column<int>(type: "int", nullable: false),
                    SubstituteTrainerId = table.Column<int>(type: "int", nullable: true),
                    RoomId = table.Column<int>(type: "int", nullable: true),
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    BookedCount = table.Column<int>(type: "int", nullable: false),
                    WaitlistCount = table.Column<int>(type: "int", nullable: false),
                    AttendedCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassSessions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassSessions_ClassFormats_ClassFormatId",
                        column: x => x.ClassFormatId,
                        principalSchema: "gym",
                        principalTable: "ClassFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassSessions_ClassSchedules_ClassScheduleId",
                        column: x => x.ClassScheduleId,
                        principalSchema: "gym",
                        principalTable: "ClassSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassSessions_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "gym",
                        principalTable: "Rooms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClassSessions_Trainers_SubstituteTrainerId",
                        column: x => x.SubstituteTrainerId,
                        principalSchema: "gym",
                        principalTable: "Trainers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClassSessions_Trainers_TrainerId",
                        column: x => x.TrainerId,
                        principalSchema: "gym",
                        principalTable: "Trainers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Meals",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DietPlanId = table.Column<int>(type: "int", nullable: false),
                    Slot = table.Column<int>(type: "int", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Items = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Calories = table.Column<int>(type: "int", nullable: false),
                    ProteinGrams = table.Column<int>(type: "int", nullable: false),
                    CarbGrams = table.Column<int>(type: "int", nullable: false),
                    FatGrams = table.Column<int>(type: "int", nullable: false),
                    TimingHint = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meals_DietPlans_DietPlanId",
                        column: x => x.DietPlanId,
                        principalSchema: "gym",
                        principalTable: "DietPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProgramDays",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkoutProgramId = table.Column<int>(type: "int", nullable: false),
                    DayIndex = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Focus = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsRestDay = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramDays_WorkoutPrograms_WorkoutProgramId",
                        column: x => x.WorkoutProgramId,
                        principalSchema: "gym",
                        principalTable: "WorkoutPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLines",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SacOrHsnCode = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxableValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GstRatePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CgstAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SgstAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IgstAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: true),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "gym",
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GatewayName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    GatewayOrderId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    GatewayPaymentId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    GatewaySignature = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    GatewayPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChequeNumber = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    BankReference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ReceivedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefundedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "gym",
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassSessionId = table.Column<int>(type: "int", nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    SubscriptionId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BookedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WasLateCancel = table.Column<bool>(type: "bit", nullable: false),
                    CheckedInAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WaitlistPosition = table.Column<int>(type: "int", nullable: true),
                    PromotedFromWaitlistAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreditConsumed = table.Column<bool>(type: "bit", nullable: false),
                    RatingScore = table.Column<int>(type: "int", nullable: true),
                    RatingComment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookings_ClassSessions_ClassSessionId",
                        column: x => x.ClassSessionId,
                        principalSchema: "gym",
                        principalTable: "ClassSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bookings_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bookings_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalSchema: "gym",
                        principalTable: "Subscriptions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CheckIns",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ClassSessionId = table.Column<int>(type: "int", nullable: true),
                    CheckInAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckOutAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VisitDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    WasBlocked = table.Column<bool>(type: "bit", nullable: false),
                    BlockReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RecordedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckIns_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "gym",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckIns_ClassSessions_ClassSessionId",
                        column: x => x.ClassSessionId,
                        principalSchema: "gym",
                        principalTable: "ClassSessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CheckIns_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainerRatings",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrainerId = table.Column<int>(type: "int", nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    ClassSessionId = table.Column<int>(type: "int", nullable: true),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainerRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainerRatings_ClassSessions_ClassSessionId",
                        column: x => x.ClassSessionId,
                        principalSchema: "gym",
                        principalTable: "ClassSessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TrainerRatings_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrainerRatings_Trainers_TrainerId",
                        column: x => x.TrainerId,
                        principalSchema: "gym",
                        principalTable: "Trainers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProgramExercises",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProgramDayId = table.Column<int>(type: "int", nullable: false),
                    ExerciseId = table.Column<int>(type: "int", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Sets = table.Column<int>(type: "int", nullable: false),
                    RepScheme = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RestSeconds = table.Column<int>(type: "int", nullable: false),
                    TargetWeightKg = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    Tempo = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SupersetGroup = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramExercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramExercises_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalSchema: "gym",
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramExercises_ProgramDays_ProgramDayId",
                        column: x => x.ProgramDayId,
                        principalSchema: "gym",
                        principalTable: "ProgramDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkoutLogs",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    ExerciseId = table.Column<int>(type: "int", nullable: false),
                    ProgramExerciseId = table.Column<int>(type: "int", nullable: true),
                    PerformedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    PerformedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SetNumber = table.Column<int>(type: "int", nullable: false),
                    Reps = table.Column<int>(type: "int", nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    Rpe = table.Column<int>(type: "int", nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    DistanceKm = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    Volume = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    EstimatedOneRepMax = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    IsPersonalRecord = table.Column<bool>(type: "bit", nullable: false),
                    PrCelebrated = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkoutLogs_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalSchema: "gym",
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkoutLogs_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkoutLogs_ProgramExercises_ProgramExerciseId",
                        column: x => x.ProgramExerciseId,
                        principalSchema: "gym",
                        principalTable: "ProgramExercises",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                schema: "auth",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "auth",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "auth",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                schema: "auth",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Badges_Slug",
                schema: "gym",
                table: "Badges",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_Slug",
                schema: "gym",
                table: "BlogPosts",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_State_PublishedAtUtc",
                schema: "gym",
                table: "BlogPosts",
                columns: new[] { "State", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BodyScans_MemberId_ScanDate",
                schema: "gym",
                table: "BodyScans",
                columns: new[] { "MemberId", "ScanDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ClassSessionId_MemberId_Status",
                schema: "gym",
                table: "Bookings",
                columns: new[] { "ClassSessionId", "MemberId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ClassSessionId_WaitlistPosition",
                schema: "gym",
                table: "Bookings",
                columns: new[] { "ClassSessionId", "WaitlistPosition" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_MemberId_BookedAtUtc",
                schema: "gym",
                table: "Bookings",
                columns: new[] { "MemberId", "BookedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SubscriptionId",
                schema: "gym",
                table: "Bookings",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_MemberCodePrefix",
                schema: "gym",
                table: "Branches",
                column: "MemberCodePrefix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Branches_Slug",
                schema: "gym",
                table: "Branches",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchStocks_BranchId_ProductId",
                schema: "gym",
                table: "BranchStocks",
                columns: new[] { "BranchId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchStocks_ProductId",
                schema: "gym",
                table: "BranchStocks",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_BranchId",
                schema: "gym",
                table: "Challenges",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_BranchId_CheckOutAtUtc",
                schema: "gym",
                table: "CheckIns",
                columns: new[] { "BranchId", "CheckOutAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_BranchId_VisitDate",
                schema: "gym",
                table: "CheckIns",
                columns: new[] { "BranchId", "VisitDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_ClassSessionId",
                schema: "gym",
                table: "CheckIns",
                column: "ClassSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_MemberId_VisitDate",
                schema: "gym",
                table: "CheckIns",
                columns: new[] { "MemberId", "VisitDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassFormats_Slug",
                schema: "gym",
                table: "ClassFormats",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassSchedules_BranchId_DayOfWeek_StartTime",
                schema: "gym",
                table: "ClassSchedules",
                columns: new[] { "BranchId", "DayOfWeek", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassSchedules_ClassFormatId",
                schema: "gym",
                table: "ClassSchedules",
                column: "ClassFormatId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSchedules_RoomId",
                schema: "gym",
                table: "ClassSchedules",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSchedules_TrainerId_DayOfWeek_StartTime",
                schema: "gym",
                table: "ClassSchedules",
                columns: new[] { "TrainerId", "DayOfWeek", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassSessions_BranchId_SessionDate_StartTime",
                schema: "gym",
                table: "ClassSessions",
                columns: new[] { "BranchId", "SessionDate", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassSessions_ClassFormatId",
                schema: "gym",
                table: "ClassSessions",
                column: "ClassFormatId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSessions_ClassScheduleId_SessionDate",
                schema: "gym",
                table: "ClassSessions",
                columns: new[] { "ClassScheduleId", "SessionDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassSessions_RoomId",
                schema: "gym",
                table: "ClassSessions",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSessions_SubstituteTrainerId",
                schema: "gym",
                table: "ClassSessions",
                column: "SubstituteTrainerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSessions_TrainerId",
                schema: "gym",
                table: "ClassSessions",
                column: "TrainerId");

            migrationBuilder.CreateIndex(
                name: "IX_CmsPages_Slug",
                schema: "gym",
                table: "CmsPages",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CmsSections_BranchId",
                schema: "gym",
                table: "CmsSections",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CmsSections_CmsPageId_Key_BranchId",
                schema: "gym",
                table: "CmsSections",
                columns: new[] { "CmsPageId", "Key", "BranchId" },
                unique: true,
                filter: "[BranchId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CmsSections_CmsPageId_OrderIndex",
                schema: "gym",
                table: "CmsSections",
                columns: new[] { "CmsPageId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_Code",
                schema: "gym",
                table: "Coupons",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DietPlans_MemberId",
                schema: "gym",
                table: "DietPlans",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_DietPlans_TrainerId",
                schema: "gym",
                table: "DietPlans",
                column: "TrainerId");

            migrationBuilder.CreateIndex(
                name: "IX_Exercises_PrimaryMuscle_Equipment",
                schema: "gym",
                table: "Exercises",
                columns: new[] { "PrimaryMuscle", "Equipment" });

            migrationBuilder.CreateIndex(
                name: "IX_Exercises_Slug",
                schema: "gym",
                table: "Exercises",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FaqItems_BranchId",
                schema: "gym",
                table: "FaqItems",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_FaqItems_Category_DisplayOrder",
                schema: "gym",
                table: "FaqItems",
                columns: new[] { "Category", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedPosts_BranchId",
                schema: "gym",
                table: "FeedPosts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedPosts_IsVisible_PostedAtUtc",
                schema: "gym",
                table: "FeedPosts",
                columns: new[] { "IsVisible", "PostedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedPosts_MemberId",
                schema: "gym",
                table: "FeedPosts",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId",
                schema: "gym",
                table: "InvoiceLines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_BranchId_IssuedOn",
                schema: "gym",
                table: "Invoices",
                columns: new[] { "BranchId", "IssuedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                schema: "gym",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_MemberId",
                schema: "gym",
                table: "Invoices",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OrderId",
                schema: "gym",
                table: "Invoices",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status_DueOn",
                schema: "gym",
                table: "Invoices",
                columns: new[] { "Status", "DueOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SubscriptionId",
                schema: "gym",
                table: "Invoices",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadFollowUps_DueAtUtc_CompletedAtUtc",
                schema: "gym",
                table: "LeadFollowUps",
                columns: new[] { "DueAtUtc", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadFollowUps_LeadId",
                schema: "gym",
                table: "LeadFollowUps",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_BranchId_Stage",
                schema: "gym",
                table: "Leads",
                columns: new[] { "BranchId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_ConvertedMemberId",
                schema: "gym",
                table: "Leads",
                column: "ConvertedMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_InterestedPlanId",
                schema: "gym",
                table: "Leads",
                column: "InterestedPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_NextFollowUpAtUtc",
                schema: "gym",
                table: "Leads",
                column: "NextFollowUpAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_Phone",
                schema: "gym",
                table: "Leads",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_Stage_CreatedAtUtc",
                schema: "gym",
                table: "Leads",
                columns: new[] { "Stage", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Meals_DietPlanId_OrderIndex",
                schema: "gym",
                table: "Meals",
                columns: new[] { "DietPlanId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_Kind_Folder",
                schema: "gym",
                table: "MediaAssets",
                columns: new[] { "Kind", "Folder" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberBadges_BadgeId",
                schema: "gym",
                table: "MemberBadges",
                column: "BadgeId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberBadges_MemberId_BadgeId",
                schema: "gym",
                table: "MemberBadges",
                columns: new[] { "MemberId", "BadgeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Members_ChurnRisk_LastVisitOn",
                schema: "gym",
                table: "Members",
                columns: new[] { "ChurnRisk", "LastVisitOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Members_HomeBranchId_Status",
                schema: "gym",
                table: "Members",
                columns: new[] { "HomeBranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Members_MemberCode",
                schema: "gym",
                table: "Members",
                column: "MemberCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Members_Phone",
                schema: "gym",
                table: "Members",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_Members_QrToken",
                schema: "gym",
                table: "Members",
                column: "QrToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Members_ReferralCode",
                schema: "gym",
                table: "Members",
                column: "ReferralCode");

            migrationBuilder.CreateIndex(
                name: "IX_Members_ReferredByMemberId",
                schema: "gym",
                table: "Members",
                column: "ReferredByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Members_UserId",
                schema: "gym",
                table: "Members",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_MemberId_IsRead_CreatedAtUtc",
                schema: "gym",
                table: "Notifications",
                columns: new[] { "MemberId", "IsRead", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                schema: "gym",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_OrderId",
                schema: "gym",
                table: "OrderLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_ProductId",
                schema: "gym",
                table: "OrderLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BranchId_PlacedAtUtc",
                schema: "gym",
                table: "Orders",
                columns: new[] { "BranchId", "PlacedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_MemberId",
                schema: "gym",
                table: "Orders",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNumber",
                schema: "gym",
                table: "Orders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BranchId_PaidAtUtc",
                schema: "gym",
                table: "Payments",
                columns: new[] { "BranchId", "PaidAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_GatewayPaymentId",
                schema: "gym",
                table: "Payments",
                column: "GatewayPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_IdempotencyKey",
                schema: "gym",
                table: "Payments",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_InvoiceId",
                schema: "gym",
                table: "Payments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_MemberId",
                schema: "gym",
                table: "Payments",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanBranchPrices_BranchId",
                schema: "gym",
                table: "PlanBranchPrices",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanBranchPrices_PlanId_BranchId",
                schema: "gym",
                table: "PlanBranchPrices",
                columns: new[] { "PlanId", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plans_ShowOnWebsite_DisplayOrder",
                schema: "gym",
                table: "Plans",
                columns: new[] { "ShowOnWebsite", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Plans_Slug",
                schema: "gym",
                table: "Plans",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                schema: "gym",
                table: "Products",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramDays_WorkoutProgramId_DayIndex",
                schema: "gym",
                table: "ProgramDays",
                columns: new[] { "WorkoutProgramId", "DayIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramExercises_ExerciseId",
                schema: "gym",
                table: "ProgramExercises",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramExercises_ProgramDayId_OrderIndex",
                schema: "gym",
                table: "ProgramExercises",
                columns: new[] { "ProgramDayId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_ProgressPhotos_MemberId_TakenOn",
                schema: "gym",
                table: "ProgressPhotos",
                columns: new[] { "MemberId", "TakenOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_Code",
                schema: "gym",
                table: "Referrals",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_InviteeMemberId",
                schema: "gym",
                table: "Referrals",
                column: "InviteeMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_LeadId",
                schema: "gym",
                table: "Referrals",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferrerMemberId",
                schema: "gym",
                table: "Referrals",
                column: "ReferrerMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                schema: "auth",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_RevokedAtUtc",
                schema: "auth",
                table: "RefreshTokens",
                columns: new[] { "UserId", "RevokedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "auth",
                table: "Roles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_BranchId_Name",
                schema: "gym",
                table: "Rooms",
                columns: new[] { "BranchId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteSettings_Key",
                schema: "gym",
                table: "SiteSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_FromBranchId",
                schema: "gym",
                table: "StockTransfers",
                column: "FromBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_ProductId",
                schema: "gym",
                table: "StockTransfers",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_Status_RequestedAtUtc",
                schema: "gym",
                table: "StockTransfers",
                columns: new[] { "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_ToBranchId",
                schema: "gym",
                table: "StockTransfers",
                column: "ToBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_BranchId",
                schema: "gym",
                table: "Subscriptions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_CouponId",
                schema: "gym",
                table: "Subscriptions",
                column: "CouponId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_MemberId_Status",
                schema: "gym",
                table: "Subscriptions",
                columns: new[] { "MemberId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_PlanId",
                schema: "gym",
                table: "Subscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_Status_EndsOn",
                schema: "gym",
                table: "Subscriptions",
                columns: new[] { "Status", "EndsOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UpgradedFromSubscriptionId",
                schema: "gym",
                table: "Subscriptions",
                column: "UpgradedFromSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Testimonials_BranchId",
                schema: "gym",
                table: "Testimonials",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Testimonials_IsVisible_DisplayOrder",
                schema: "gym",
                table: "Testimonials",
                columns: new[] { "IsVisible", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_TrainerRatings_ClassSessionId",
                schema: "gym",
                table: "TrainerRatings",
                column: "ClassSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerRatings_MemberId",
                schema: "gym",
                table: "TrainerRatings",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerRatings_TrainerId_CreatedAtUtc",
                schema: "gym",
                table: "TrainerRatings",
                columns: new[] { "TrainerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Trainers_PrimaryBranchId_IsActive",
                schema: "gym",
                table: "Trainers",
                columns: new[] { "PrimaryBranchId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Trainers_Slug",
                schema: "gym",
                table: "Trainers",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trainers_UserId",
                schema: "gym",
                table: "Trainers",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Transformations_BranchId",
                schema: "gym",
                table: "Transformations",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Transformations_IsVisible_ConsentGiven_DisplayOrder",
                schema: "gym",
                table: "Transformations",
                columns: new[] { "IsVisible", "ConsentGiven", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Transformations_MemberId",
                schema: "gym",
                table: "Transformations",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "auth",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Users_HomeBranchId",
                schema: "auth",
                table: "Users",
                column: "HomeBranchId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "auth",
                table: "Users",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutLogs_ExerciseId",
                schema: "gym",
                table: "WorkoutLogs",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutLogs_MemberId_ExerciseId_EstimatedOneRepMax",
                schema: "gym",
                table: "WorkoutLogs",
                columns: new[] { "MemberId", "ExerciseId", "EstimatedOneRepMax" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutLogs_MemberId_PerformedOn",
                schema: "gym",
                table: "WorkoutLogs",
                columns: new[] { "MemberId", "PerformedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutLogs_ProgramExerciseId",
                schema: "gym",
                table: "WorkoutLogs",
                column: "ProgramExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutPrograms_MemberId_Status",
                schema: "gym",
                table: "WorkoutPrograms",
                columns: new[] { "MemberId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutPrograms_TrainerId",
                schema: "gym",
                table: "WorkoutPrograms",
                column: "TrainerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "BlogPosts",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "BodyScans",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Bookings",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "BranchStocks",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Challenges",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "CheckIns",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "CmsSections",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "FaqItems",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "FeedPosts",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "InvoiceLines",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "LeadFollowUps",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Meals",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "MediaAssets",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "MemberBadges",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "OrderLines",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "PlanBranchPrices",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "ProgressPhotos",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Referrals",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "RefreshTokens",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "SiteSettings",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "StockTransfers",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Testimonials",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "TrainerRatings",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Transformations",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "WorkoutLogs",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "CmsPages",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "DietPlans",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Badges",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Invoices",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Leads",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "ClassSessions",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "ProgramExercises",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Subscriptions",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "ClassSchedules",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Exercises",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "ProgramDays",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Coupons",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Plans",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "ClassFormats",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Rooms",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "WorkoutPrograms",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Members",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Trainers",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "Branches",
                schema: "gym");
        }
    }
}
