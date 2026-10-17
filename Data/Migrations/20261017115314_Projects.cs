using System;
using Microsoft.EntityFrameworkCore.Migrations;
using QuireClone.Domain;

#nullable disable

namespace QuireClone.Data.Migrations
{
    /// <summary>
    /// Items, statuses, custom fields and views now belong to a project. Everything that exists so far moves
    /// into the first project; status and field names only have to be unique within a project.
    /// </summary>
    public partial class Projects : Migration
    {
        private static readonly string[] ProjectTables = ["ViewConfigurations", "TaskStatuses", "Tasks", "CustomFields"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Projects",
                columns: ["Id", "Name", "SortOrder", "CreatedAt"],
                values: new object[] { DefaultProject.Id, DefaultProject.Name, 0, DateTimeOffset.UtcNow });

            migrationBuilder.DropIndex(
                name: "IX_TaskStatuses_Name",
                table: "TaskStatuses");

            migrationBuilder.DropIndex(
                name: "IX_CustomFields_Name",
                table: "CustomFields");

            // The default puts every existing row into the first project.
            foreach (var table in ProjectTables)
                migrationBuilder.AddColumn<Guid>(
                    name: "ProjectId",
                    table: table,
                    type: "TEXT",
                    nullable: false,
                    defaultValue: DefaultProject.Id);

            migrationBuilder.CreateIndex(
                name: "IX_ViewConfigurations_ProjectId",
                table: "ViewConfigurations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskStatuses_ProjectId_Name",
                table: "TaskStatuses",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId",
                table: "Tasks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFields_ProjectId_Name",
                table: "CustomFields",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Name",
                table: "Projects",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomFields_Projects_ProjectId",
                table: "CustomFields",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Projects_ProjectId",
                table: "Tasks",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskStatuses_Projects_ProjectId",
                table: "TaskStatuses",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ViewConfigurations_Projects_ProjectId",
                table: "ViewConfigurations",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomFields_Projects_ProjectId",
                table: "CustomFields");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Projects_ProjectId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskStatuses_Projects_ProjectId",
                table: "TaskStatuses");

            migrationBuilder.DropForeignKey(
                name: "FK_ViewConfigurations_Projects_ProjectId",
                table: "ViewConfigurations");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_ViewConfigurations_ProjectId",
                table: "ViewConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_TaskStatuses_ProjectId_Name",
                table: "TaskStatuses");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProjectId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_CustomFields_ProjectId_Name",
                table: "CustomFields");

            // Names may repeat across projects; going back only works once they are unique again.
            foreach (var table in ProjectTables)
                migrationBuilder.DropColumn(
                    name: "ProjectId",
                    table: table);

            migrationBuilder.CreateIndex(
                name: "IX_TaskStatuses_Name",
                table: "TaskStatuses",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomFields_Name",
                table: "CustomFields",
                column: "Name",
                unique: true);
        }
    }
}
