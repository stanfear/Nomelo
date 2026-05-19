using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nomelo.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lists",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    file_path = table.Column<string>(type: "text", nullable: false),
                    item_count = table.Column<int>(type: "integer", nullable: false),
                    loaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "voting_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    list_id = table.Column<string>(type: "text", nullable: false),
                    confidence_threshold = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    share_token = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voting_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_voting_sessions_lists_list_id",
                        column: x => x.list_id,
                        principalTable: "lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_states",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item = table.Column<string>(type: "text", nullable: false),
                    elo_score = table.Column<double>(type: "double precision", nullable: false, defaultValue: 1000.0),
                    times_shown = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_banned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_states", x => new { x.session_id, x.item });
                    table.ForeignKey(
                        name: "FK_item_states_voting_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "voting_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "votes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_a = table.Column<string>(type: "text", nullable: false),
                    item_b = table.Column<string>(type: "text", nullable: false),
                    result = table.Column<string>(type: "text", nullable: false),
                    presented_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_votes", x => x.id);
                    table.ForeignKey(
                        name: "FK_votes_voting_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "voting_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_votes_session_id",
                table: "votes",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_voting_sessions_list_id",
                table: "voting_sessions",
                column: "list_id");

            migrationBuilder.CreateIndex(
                name: "IX_voting_sessions_share_token",
                table: "voting_sessions",
                column: "share_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_voting_sessions_user_id_list_id",
                table: "voting_sessions",
                columns: new[] { "user_id", "list_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_states");

            migrationBuilder.DropTable(
                name: "votes");

            migrationBuilder.DropTable(
                name: "voting_sessions");

            migrationBuilder.DropTable(
                name: "lists");
        }
    }
}
