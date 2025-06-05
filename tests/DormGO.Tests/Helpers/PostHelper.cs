using DormGO.Models;

namespace DormGO.Tests.Helpers;

public static class PostHelper
{
    public static Post CreatePost(ApplicationUser? creator = null)
    {
        const int testMaxPeople = 5;
        return new Post
        {
            Id = "test_post_id",
            Title = "title",
            Description = "description",
            Latitude = 12,
            Longitude = 1234,
            CurrentPrice = 12345.678m,
            CreatedAt = DateTime.UtcNow,
            MaxPeople = 5,
            CreatorId = creator?.Id ?? "test_user_id",
            Members = new List<ApplicationUser>()
        };
    }
}