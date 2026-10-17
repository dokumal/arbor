using Microsoft.EntityFrameworkCore;
using QuireClone.Domain;

namespace QuireClone.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<WorkspaceItem> Items => Set<WorkspaceItem>();
    public DbSet<ItemLink> ItemLinks => Set<ItemLink>();
    public DbSet<TaskStatusDefinition> Statuses => Set<TaskStatusDefinition>();
    public DbSet<CustomFieldDefinition> CustomFields => Set<CustomFieldDefinition>();
    public DbSet<ItemFieldValue> ItemFieldValues => Set<ItemFieldValue>();
    public DbSet<ViewConfiguration> ViewConfigurations => Set<ViewConfiguration>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        // Items, statuses, fields and views belong to exactly one project. ProjectService deletes a project's
        // data in dependency order, because the database cascade would hit the restricted status reference first.
        modelBuilder.Entity<WorkspaceItem>().HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TaskStatusDefinition>().HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CustomFieldDefinition>().HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ViewConfiguration>().HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WorkspaceItem>(entity =>
        {
            // Table and column names date from when every item was a task.
            entity.ToTable("Tasks");
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.ParentId).HasColumnName("ParentTaskId");
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Description).HasColumnType("text");
            // A status in use can't be deleted; StatusService moves its tasks to another status first.
            entity.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ParentItem).WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_Tasks_Tasks_ParentTaskId");
            entity.HasIndex(x => new { x.ParentId, x.SortOrder }).HasDatabaseName("IX_Tasks_ParentTaskId_SortOrder");
            entity.HasIndex(x => x.DueDate);
        });

        modelBuilder.Entity<TaskStatusDefinition>(entity =>
        {
            entity.ToTable("TaskStatuses");
            entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique();
            entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Color).HasMaxLength(16).IsRequired();
        });

        modelBuilder.Entity<CustomFieldDefinition>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Options).HasJsonConversion("OptionsJson");
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
            entity.Property(x => x.Filters).HasJsonConversion("FiltersJson");
            entity.Property(x => x.VisibleFields).HasJsonConversion("VisibleFieldsJson");
            entity.Property(x => x.GroupByField).HasMaxLength(64);
            entity.Property(x => x.SortField).HasMaxLength(64);
        });

        modelBuilder.Entity<TimeEntry>(entity =>
        {
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => new { x.Date, x.Start });
        });
    }
}
