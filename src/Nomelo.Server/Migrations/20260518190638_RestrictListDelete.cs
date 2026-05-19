using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nomelo.Server.Migrations
{
    /// <inheritdoc />
    public partial class RestrictListDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_voting_sessions_lists_list_id",
                table: "voting_sessions");

            migrationBuilder.AddForeignKey(
                name: "FK_voting_sessions_lists_list_id",
                table: "voting_sessions",
                column: "list_id",
                principalTable: "lists",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_voting_sessions_lists_list_id",
                table: "voting_sessions");

            migrationBuilder.AddForeignKey(
                name: "FK_voting_sessions_lists_list_id",
                table: "voting_sessions",
                column: "list_id",
                principalTable: "lists",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
