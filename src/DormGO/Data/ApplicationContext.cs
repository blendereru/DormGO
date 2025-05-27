using DormGO.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DormGO.Data;

public class ApplicationContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationContext(DbContextOptions<ApplicationContext> opts) : base(opts) { }
    public DbSet<Post> Posts { get; set; } = null!;
    public DbSet<RefreshSession> RefreshSessions { get; set; } = null!;
    public DbSet<UserConnection> UserConnections { get; set; } = null!;
    public DbSet<Message> Messages { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<PostNotification> PostNotifications { get; set; } = null!;
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>().Ignore(u => u.Fingerprint);
        builder.Entity<RefreshSession>()
            .HasOne(r => r.User)
            .WithMany(u => u.RefreshSessions)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<RefreshSession>(r =>
        {
            r.Property(p => p.Fingerprint).HasMaxLength(200);
            r.Property(p => p.UA).HasMaxLength(200);
            // r.Property(p => p.Ip).HasMaxLength(15);
        });
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
        builder.Entity<Post>()
            .HasOne(p => p.Creator)
            .WithMany(u => u.CreatedPosts)
            .OnDelete(DeleteBehavior.NoAction);
        builder.Entity<UserConnection>(x =>
        {
            x.HasKey(p => p.ConnectionId);
            x.HasOne(uc => uc.User)
                .WithMany(u => u.UserConnections)
                .OnDelete(DeleteBehavior.Cascade);
            // x.Property(p => p.Ip).HasMaxLength(15);
        });
        builder.Entity<Message>()
            .HasOne(m => m.Post)
            .WithMany(p => p.Messages)
            .HasForeignKey(m => m.PostId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Notification>().UseTpcMappingStrategy();
        builder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<PostNotification>()
            .HasOne(pn => pn.Post)
            .WithMany()
            .HasForeignKey(pn => pn.PostId);
    }
}