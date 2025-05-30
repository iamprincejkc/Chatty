using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatty.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionToChatMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SenderRole",
                table: "ChatMessages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "ChatMessages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SenderRole",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "ChatMessages");
        }
    }
}
