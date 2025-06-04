using DormGO.Data;
using DormGO.Models;

namespace DormGO.Tests.Helpers;

public static class DataSeedHelper
{
    public static async Task SeedPostDataAsync(ApplicationContext db, ApplicationUser creator, bool saveChanges = true)
    {
        var post1 = new Post
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title1",
            Description = "description1",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow,
            MaxPeople = 0,
            CreatorId = creator.Id
        };
        var post2 = new Post
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title2",
            Description = "description2",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            MaxPeople = 0,
            CreatorId = creator.Id
        };
        var post3 = new Post
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title3",
            Description = "description3",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            CreatorId = creator.Id
        };
        var post4 = new Post
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title4",
            Description = "description4",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            CreatorId = creator.Id       
        };
        var post5 = new Post
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title5",
            Description = "description5",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow.AddDays(-4),
            CreatorId = creator.Id       
        };
        await db.Posts.AddRangeAsync(post1, post2, post3, post4, post5);
        if (saveChanges)
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    public async static Task SeedPostNotificationData(ApplicationContext db, ApplicationUser user, Post post, bool saveChanges = true)
    {
        var notification1 = new PostNotification
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title1",
            Description = "description1",
            UserId = user.Id,
            PostId = post.Id
        };
        var notification2 = new PostNotification
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title2",
            Description = "description2",
            UserId = user.Id,
            PostId = post.Id
        };
        var notification3 = new PostNotification
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title3",
            Description = "description3",
            UserId = user.Id,
            PostId = post.Id
        };
        var notification4 = new PostNotification
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title4",
            Description = "description4",
            UserId = user.Id,
            PostId = post.Id
        };
        var notification5 = new PostNotification
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title5",
            Description = "description5",
            UserId = user.Id,
            PostId = post.Id
        };
        await db.PostNotifications.AddRangeAsync(notification1, notification2, notification3, notification4, notification5);
        if (saveChanges)
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }
}