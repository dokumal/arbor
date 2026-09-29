using Microsoft.EntityFrameworkCore;
using QuireClone.Domain;

namespace QuireClone.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<CustomFieldDefinition> CustomFields => Set<CustomFieldDefinition>();
    public DbSet<TaskFieldValue> TaskFieldValues => Set<TaskFieldValue>();
    public DbSet<ViewConfiguration> ViewConfigurations => Set<ViewConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Description).HasColumnType("text");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasOne(x => x.ParentTask).WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentTaskId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.ParentTaskId, x.SortOrder });
            entity.HasIndex(x => x.DueDate);
        });

        modelBuilder.Entity<CustomFieldDefinition>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.OptionsJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<TaskFieldValue>(entity =>
        {
            entity.HasKey(x => new { x.TaskId, x.FieldId });
            entity.Property(x => x.Value).HasColumnType("text");
            entity.HasOne(x => x.Task).WithMany(x => x.FieldValues)
                .HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Field).WithMany(x => x.Values)
                .HasForeignKey(x => x.FieldId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.FieldId, x.Value });
        });

        modelBuilder.Entity<ViewConfiguration>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.FiltersJson).HasColumnType("jsonb");
            entity.Property(x => x.VisibleFieldsJson).HasColumnType("jsonb");
            entity.Property(x => x.GroupByField).HasMaxLength(64);
        });
    }
}
