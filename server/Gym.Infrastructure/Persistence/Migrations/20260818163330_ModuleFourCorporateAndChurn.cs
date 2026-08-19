using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ModuleFourCorporateAndChurn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChurnReasons",
                schema: "gym",
                table: "Members",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ChurnScoredAtUtc",
                schema: "gym",
                table: "Members",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CorporateAccountId",
                schema: "gym",
                table: "Members",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastWinBackAtUtc",
                schema: "gym",
                table: "Members",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CorporateAccounts",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    HrContactName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    HrContactEmail = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    HrContactPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WaiveAdmissionFee = table.Column<bool>(type: "bit", nullable: false),
                    SeatCap = table.Column<int>(type: "int", nullable: true),
                    SeatsUsed = table.Column<int>(type: "int", nullable: false),
                    BranchScope = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CorporateEnrolments",
                schema: "gym",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CorporateAccountId = table.Column<int>(type: "int", nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    WorkEmail = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    EnrolledOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateEnrolments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorporateEnrolments_CorporateAccounts_CorporateAccountId",
                        column: x => x.CorporateAccountId,
                        principalSchema: "gym",
                        principalTable: "CorporateAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CorporateEnrolments_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "gym",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Members_CorporateAccountId",
                schema: "gym",
                table: "Members",
                column: "CorporateAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateAccounts_Code",
                schema: "gym",
                table: "CorporateAccounts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorporateAccounts_IsActive_ValidTo",
                schema: "gym",
                table: "CorporateAccounts",
                columns: new[] { "IsActive", "ValidTo" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateEnrolments_CorporateAccountId_MemberId_IsActive",
                schema: "gym",
                table: "CorporateEnrolments",
                columns: new[] { "CorporateAccountId", "MemberId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateEnrolments_MemberId",
                schema: "gym",
                table: "CorporateEnrolments",
                column: "MemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_Members_CorporateAccounts_CorporateAccountId",
                schema: "gym",
                table: "Members",
                column: "CorporateAccountId",
                principalSchema: "gym",
                principalTable: "CorporateAccounts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Members_CorporateAccounts_CorporateAccountId",
                schema: "gym",
                table: "Members");

            migrationBuilder.DropTable(
                name: "CorporateEnrolments",
                schema: "gym");

            migrationBuilder.DropTable(
                name: "CorporateAccounts",
                schema: "gym");

            migrationBuilder.DropIndex(
                name: "IX_Members_CorporateAccountId",
                schema: "gym",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "ChurnReasons",
                schema: "gym",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "ChurnScoredAtUtc",
                schema: "gym",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "CorporateAccountId",
                schema: "gym",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "LastWinBackAtUtc",
                schema: "gym",
                table: "Members");
        }
    }
}
