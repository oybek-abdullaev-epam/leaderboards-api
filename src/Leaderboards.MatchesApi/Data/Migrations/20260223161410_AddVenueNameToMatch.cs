using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leaderboards.MatchesApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVenueNameToMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VenueName",
                table: "Matches",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VenueName",
                table: "Matches");
        }
    }
}
