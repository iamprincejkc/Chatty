using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatty.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentConnectionIdToAgentSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgentConnectionId",
                table: "AgentSessions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgentConnectionId",
                table: "AgentSessions");
        }
    }
}
