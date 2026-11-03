using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Emlak.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvisors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdvisorId",
                table: "Listings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Advisors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    WhatsApp = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Advisors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_AdvisorId",
                table: "Listings",
                column: "AdvisorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Listings_Advisors_AdvisorId",
                table: "Listings",
                column: "AdvisorId",
                principalTable: "Advisors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Listings_Advisors_AdvisorId",
                table: "Listings");

            migrationBuilder.DropTable(
                name: "Advisors");

            migrationBuilder.DropIndex(
                name: "IX_Listings_AdvisorId",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "AdvisorId",
                table: "Listings");
        }
    }
}
