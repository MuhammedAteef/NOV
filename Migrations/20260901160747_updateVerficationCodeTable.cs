using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class updateVerficationCodeTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VerificationCodes_AspNetUsers_UserId1",
                table: "VerificationCodes");

            migrationBuilder.DropIndex(
                name: "IX_VerificationCodes_UserId1",
                table: "VerificationCodes");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "VerificationCodes");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "VerificationCodes",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationCodes_UserId",
                table: "VerificationCodes",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_VerificationCodes_AspNetUsers_UserId",
                table: "VerificationCodes",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VerificationCodes_AspNetUsers_UserId",
                table: "VerificationCodes");

            migrationBuilder.DropIndex(
                name: "IX_VerificationCodes_UserId",
                table: "VerificationCodes");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "VerificationCodes",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "UserId1",
                table: "VerificationCodes",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationCodes_UserId1",
                table: "VerificationCodes",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_VerificationCodes_AspNetUsers_UserId1",
                table: "VerificationCodes",
                column: "UserId1",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
