using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nomelo.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "voting_sessions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "name",
                table: "voting_sessions");
        }
    }
}
