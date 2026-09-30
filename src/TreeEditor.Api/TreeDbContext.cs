using Microsoft.EntityFrameworkCore;

namespace TreeEditor.Api;

public class TreeDbContext(DbContextOptions<TreeDbContext> options) : DbContext(options)
{
    public DbSet<Node> Nodes => Set<Node>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Node>(node =>
        {
            node.ToTable("nodes");
            node.HasKey(n => n.Id).HasName("pk_nodes");
            node.Property(n => n.Id).HasColumnName("id");
            node.Property(n => n.ParentId).HasColumnName("parent_id");
            node.Property(n => n.Value).HasColumnName("value").HasMaxLength(Node.MaxValueLength);
            node.Property(n => n.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            node.Property(n => n.DeletedAt).HasColumnName("deleted_at");

            node.HasOne(n => n.Parent)
                .WithMany()
                .HasForeignKey(n => n.ParentId)
                .HasConstraintName("fk_nodes_parent_id")
                .OnDelete(DeleteBehavior.Restrict);
            node.HasIndex(n => n.ParentId).HasDatabaseName("ix_nodes_parent_id");
        });
    }
}
