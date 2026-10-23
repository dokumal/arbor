using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuireClone.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveItemLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemLinks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItemLinks",
                columns: table => new
                {
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemLinks", x => new { x.TaskId, x.DocumentId });
                    table.ForeignKey(
                        name: "FK_ItemLinks_Tasks_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemLinks_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemLinks_DocumentId",
                table: "ItemLinks",
                column: "DocumentId");
        }
    }
}
