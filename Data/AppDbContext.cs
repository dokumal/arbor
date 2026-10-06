using Microsoft.EntityFrameworkCore;
using QuireClone.Domain;

namespace QuireClone.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<WorkspaceItem> Items => Set<WorkspaceItem>();
    public DbSet<ItemLink> ItemLinks => Set<ItemLink>();
    public DbSet<CustomFieldDefinition> CustomFields => Set<CustomFieldDefinition>();
    public DbSet<ItemFieldValue> ItemFieldValues => Set<ItemFieldValue>();
    public DbSet<ViewConfiguration> ViewConfigurations => Set<ViewConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkspaceItem>(entity =>
        {
            // Keep the existing table and columns so stored tasks retain their IDs and hierarchy.
            entity.ToTable("Tasks");
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.ParentId).HasColumnName("ParentTaskId");
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Description).HasColumnType("text");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasOne(x => x.ParentItem).WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_Tasks_Tasks_ParentTaskId");
            entity.HasIndex(x => new { x.ParentId, x.SortOrder }).HasDatabaseName("IX_Tasks_ParentTaskId_SortOrder");
            entity.HasIndex(x => x.DueDate);
        });

        modelBuilder.Entity<CustomFieldDefinition>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.OptionsJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<ItemFieldValue>(entity =>
        {
            entity.ToTable("TaskFieldValues");
            entity.Property(x => x.ItemId).HasColumnName("TaskId");
            entity.HasKey(x => new { x.ItemId, x.FieldId });
            entity.Property(x => x.Value).HasColumnType("text");
            entity.HasOne(x => x.Item).WithMany(x => x.FieldValues)
                .HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_TaskFieldValues_Tasks_TaskId");
            entity.HasOne(x => x.Field).WithMany(x => x.Values)
                .HasForeignKey(x => x.FieldId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.FieldId, x.Value });
        });

        modelBuilder.Entity<ItemLink>(entity =>
        {
            entity.HasKey(x => new { x.TaskId, x.DocumentId });
            entity.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ViewConfiguration>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.FiltersJson).HasColumnType("jsonb");
            entity.Property(x => x.VisibleFieldsJson).HasColumnType("jsonb");
            entity.Property(x => x.GroupByField).HasMaxLength(64);
            entity.Property(x => x.SortField).HasMaxLength(64);
        });
    }
}
