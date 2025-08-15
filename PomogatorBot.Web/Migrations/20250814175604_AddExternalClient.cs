using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PomogatorBot.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "external_clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_admin_user_id = table.Column<long>(type: "bigint", nullable: true),
                    revoked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_used_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usage_count = table.Column<long>(type: "bigint", nullable: false),
                    key_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_clients", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_external_clients_key_hash",
                table: "external_clients",
                column: "key_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "external_clients");
        }
    }
}
