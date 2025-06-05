using DormGO.Models;

namespace DormGO.Tests.Helpers;

public static class PostHelper
{
    public static Post CreatePost(ApplicationUser? creator = null)
    {
        const int testMaxPeople = 5;
        return new Post
        {
            Title = "title",
            Description = "description",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow,
            CreatorId = creator?.Id ?? "test_user_id",
            MaxPeople = testMaxPeople
        };
    }
}