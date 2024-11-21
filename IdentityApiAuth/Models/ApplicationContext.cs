using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IdentityApiAuth.Models;

public class ApplicationContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationContext(DbContextOptions<ApplicationContext> opts) : base(opts) { }
    public DbSet<Post> Posts { get; set; }
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Post>(entity =>
        {
            entity.Property(p => p.Latitude)
                .HasColumnType("decimal(9,6)");
            entity.Property(p => p.Longitude)
                .HasColumnType("decimal(9,6)");
        });
        builder.Entity<Post>()
            .HasMany(p => p.Members)
            .WithMany(u => u.Posts);
    }
}