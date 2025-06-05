using DormGO.Data;
using DormGO.Models;

namespace DormGO.Tests.Helpers;

public static class DataSeedHelper
{
    public static async Task<ApplicationUser> SeedUserDataAsync(ApplicationContext db)
    {
        var user = new ApplicationUser
        {
            UserName = "user",
            Email = "user@example.com",
            NormalizedUserName = "USER",
            NormalizedEmail = "USER@EXAMPLE.COM",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("D")
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    public static async Task<List<ApplicationUser>> SeedUserDataAsync(ApplicationContext db, int maxCount)
    {
        var users = new List<ApplicationUser>();
        for (var i = 0; i < maxCount; i++)
        {
            var user = new ApplicationUser
            {
                UserName = $"user{i}",
                Email = $"user{i}@example.com",
                NormalizedUserName = $"USER{i}",
                NormalizedEmail = $"USER{i}@EXAMPLE.COM",
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString("D")
            };
            users.Add(user);
        }

        db.Users.AddRange(users);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return users;
    }

    public static async Task<Post> SeedPostDataAsync(ApplicationContext db, ApplicationUser creator)
    {
        var post = new Post
        {
            Title = "title",
            Description = "description",
            Latitude = 12,
            Longitude = 1234,
            CurrentPrice = 12345.678m,
            CreatedAt = DateTime.UtcNow,
            MaxPeople = 5,
            CreatorId = creator.Id,
            Members = new List<ApplicationUser>()
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return post;
    }

    public static async Task<List<Post>> SeedPostDataAsync(ApplicationContext db, ApplicationUser creator, int maxCount)
    {
        var posts = new List<Post>();
        for (var i = 0; i < maxCount; i++)
        {
            var post = new Post
            {
                Title = $"title{i}",
                Description = $"description{i}",
                Latitude = 12,
                Longitude = 1234,
                CurrentPrice = 12345.678m,
                CreatedAt = DateTime.UtcNow,
                MaxPeople = 5,
                CreatorId = creator.Id
            };
            posts.Add(post);
        }

        db.Posts.AddRange(posts);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return posts;
    }

    public static async Task<PostNotification> SeedPostNotificationData(ApplicationContext db, ApplicationUser user, Post post)
    {
        var notification = new PostNotification
        {
            Title = "title",
            Description = "description",
            UserId = user.Id,
            PostId = post.Id
        };
        db.PostNotifications.Add(notification);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return notification;
    }

    public static async Task<List<PostNotification>> SeedPostNotificationData(ApplicationContext db, ApplicationUser user, Post post, int maxCount)
    {
        var notifications = new List<PostNotification>();
        for (var i = 0; i < maxCount; i++)
        {
            var notification = new PostNotification
            {
                Title = $"title{i}",
                Description = $"description{i}",
                UserId = user.Id,
                PostId = post.Id
            };
            notifications.Add(notification);
        }

        db.PostNotifications.AddRange(notifications);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return notifications;
    }
}