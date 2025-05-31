using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatty.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIpAddressToChatMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "ChatMessages",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "ChatMessages");
        }
    }
}
