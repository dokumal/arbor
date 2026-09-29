using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace QuireClone.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260924120000_ViewGrouping")]
public partial class ViewGrouping : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "GroupByField",
            table: "ViewConfigurations",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "GroupByField", table: "ViewConfigurations");
    }
}
