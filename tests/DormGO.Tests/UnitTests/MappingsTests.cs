using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Models;
using DormGO.Mappings;
using Mapster;

namespace DormGO.Tests.UnitTests;

public class MappingsTests
{
    static MappingsTests()
    {
        MapsterConfig.Configure();
    }

    [Fact]
    public void Should_Map_UserRegisterRequest_To_ApplicationUser()
    {
        var source = new UserRegisterRequest
        {
            Email = "test@example.com",
            Name = "John Doe",
            VisitorId = "fingerprint123"
        };

        var result = source.Adapt<ApplicationUser>();

        Assert.Equal("test@example.com", result.Email);
        Assert.Equal("John Doe", result.UserName);
        Assert.Equal("fingerprint123", result.Fingerprint);
    }

    [Fact]
    public void Should_Map_UserLoginRequest_To_ApplicationUser()
    {
        var source = new UserLoginRequest
        {
            Email = "login@example.com",
            Name = "LoginUser",
            VisitorId = "fingerprint456"
        };

        var result = source.Adapt<ApplicationUser>();

        Assert.Equal("login@example.com", result.Email);
        Assert.Equal("LoginUser", result.UserName);
        Assert.Equal("fingerprint456", result.Fingerprint);
    }

    [Fact]
    public void Should_Map_ApplicationUser_To_UserResponse()
    {
        var user = new ApplicationUser
        {
            Id = "user-id",
            Email = "user@example.com",
            UserName = "user123"
        };

        var result = user.Adapt<UserResponse>();

        Assert.Equal("user-id", result.Id);
        Assert.Equal("user@example.com", result.Email);
        Assert.Equal("user123", result.Name);
    }

    [Fact]
    public void Should_Map_PostCreateRequest_To_Post()
    {
        var source = new PostCreateRequest
        {
            Title = "Test Post",
            Description = "Test Description",
            CurrentPrice = 1200,
            Latitude = 41.0082,
            Longitude = 28.9784,
            MaxPeople = 3,
            CreatedAt = DateTime.UtcNow
        };

        var result = source.Adapt<Post>();

        Assert.Equal("Test Post", result.Title);
        Assert.Equal("Test Description", result.Description);
        Assert.Equal(1200, result.CurrentPrice);
        Assert.Equal(41.0082, result.Latitude);
        Assert.Equal(28.9784, result.Longitude);
        Assert.Equal(3, result.MaxPeople);
        Assert.Equal(source.CreatedAt, result.CreatedAt);
    }

    [Fact]
    public void Should_Map_PostUpdateRequest_To_Post_Ignoring_Nulls()
    {
        var source = new PostUpdateRequest
        {
            Title = "Updated Post",
            Description = null,
            CurrentPrice = 1500,
            Latitude = 40.0,
            Longitude = 30.0,
            MaxPeople = 4
        };

        var result = source.Adapt<Post>();

        Assert.Equal("Updated Post", result.Title);
        Assert.Null(result.Description); // still allowed if set to null
        Assert.Equal(1500, result.CurrentPrice);
        Assert.Equal(40.0, result.Latitude);
        Assert.Equal(30.0, result.Longitude);
        Assert.Equal(4, result.MaxPeople);
    }

    [Fact]
    public void Should_Map_Post_To_PostResponse()
    {
        var post = new Post
        {
            Id = "test_post_id",
            Title = "My Post",
            Description = "Desc",
            CurrentPrice = 1000,
            Latitude = 10,
            Longitude = 20,
            MaxPeople = 2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Creator = new ApplicationUser { Id = "creator-id", Email = "creator@mail.com", UserName = "Creator" },
            Members = new List<ApplicationUser>
            {
                new() { Id = "u1", Email = "u1@mail.com", UserName = "u1" },
                new() { Id = "u2", Email = "u2@mail.com", UserName = "u2" }
            }
        };

        var result = post.Adapt<PostResponse>();

        Assert.Equal("test_post_id", result.Id);
        Assert.Equal("My Post", result.Title);
        Assert.Equal("Desc", result.Description);
        Assert.Equal(1000, result.CurrentPrice);
        Assert.Equal(10, result.Latitude);
        Assert.Equal(20, result.Longitude);
        Assert.Equal(2, result.MaxPeople);
        Assert.Equal(2, result.Members.Count);
        Assert.Equal("Creator", result.Creator.Name);
    }

    [Fact]
    public void Should_Map_MessageCreateRequest_To_Message()
    {
        var source = new MessageCreateRequest
        {
            Content = "Hello World",
            SentAt = DateTime.UtcNow
        };

        var result = source.Adapt<Message>();

        Assert.Equal("Hello World", result.Content);
        Assert.Equal(source.SentAt, result.SentAt);
    }

    [Fact]
    public void Should_Map_Message_To_MessageResponse()
    {
        var message = new Message
        {
            Id = "test_message_id",
            Content = "Hi",
            SentAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Sender = new ApplicationUser { Id = "1", Email = "s@mail.com", UserName = "Sender" },
            Post = new Post { Id = "test_post_id", Title = "Ad Title", Description = "Ad Description" }
        };

        var result = message.Adapt<MessageResponse>();

        Assert.Equal("Hi", result.Content);
        Assert.Equal("Sender", result.Sender.Name);
        Assert.Equal("Ad Title", result.Post.Title);
    }

    [Fact]
    public void Should_Map_PostNotification_To_PostNotificationResponse()
    {
        var postNotif = new PostNotification
        {
            Id = "test_post_notification_id",
            Title = "New post alert",
            Description = "A post you follow is updated",
            CreatedAt = DateTime.UtcNow,
            IsRead = false,
            Post = new Post { Id = "test_post_id", Title = "Dorm for Rent", Description = "Nice place" },
            User = new ApplicationUser { Id = "pid", Email = "p@mail.com", UserName = "Poster" }
        };

        var result = postNotif.Adapt<PostNotificationResponse>();

        Assert.Equal("test_post_id", result.Post.Id);
        Assert.Equal("Poster", result.User.Name);
        Assert.Equal("New post alert", result.Title);
    }

    [Fact]
    public void Should_Map_ApplicationUser_To_ProfileResponse()
    {
        var user = new ApplicationUser
        {
            Id = "uid",
            Email = "profile@mail.com",
            UserName = "ProfileUser",
            RegistrationDate = new DateTime(2023, 1, 1)
        };

        var result = user.Adapt<ProfileResponse>();

        Assert.Equal("uid", result.Id);
        Assert.Equal("profile@mail.com", result.Email);
        Assert.Equal("ProfileUser", result.Username);
        Assert.Equal(new DateTime(2023, 1, 1), result.RegisteredAt);
    }
}
