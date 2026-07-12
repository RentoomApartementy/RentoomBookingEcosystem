using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Blog.Models;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Blog.Database;

public class RappBlogReadDbContext : DbContext
{
    public RappBlogReadDbContext(DbContextOptions<RappBlogReadDbContext> options) : base(options)
    {
    }

    public DbSet<BlogPostReadEntity> BlogPosts { get; set; }
    public DbSet<BlogPostVersionReadEntity> BlogPostVersions { get; set; }
    public DbSet<BlogPostBlockReadEntity> BlogPostBlocks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BlogPostReadEntity>().HasKey(x => x.Id);
        modelBuilder.Entity<BlogPostVersionReadEntity>().HasKey(x => x.Id);
        modelBuilder.Entity<BlogPostBlockReadEntity>().HasKey(x => x.Id);

        modelBuilder.Entity<BlogPostReadEntity>().HasIndex(x => x.Slug);
        modelBuilder.Entity<BlogPostVersionReadEntity>().HasIndex(x => new { x.BlogPostId, x.VersionNo });
        modelBuilder.Entity<BlogPostBlockReadEntity>().HasIndex(x => new { x.PostVersionId, x.SortOrder });
    }
}
