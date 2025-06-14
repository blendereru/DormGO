using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Models;
using Mapster;

namespace DormGO.Mappings;
public static class MapsterConfig
{
    public static void Configure()
    {
        TypeAdapterConfig<UserRegisterRequest, ApplicationUser>.NewConfig()
            .Map(dest => dest.Email, src => src.Email)
            .Map(dest => dest.UserName, src => src.Name ?? src.Email)
            .Map(dest => dest.Fingerprint, src => src.VisitorId);
        TypeAdapterConfig<UserLoginRequest, ApplicationUser>.NewConfig()
            .Map(dest => dest.Email, src => src.Email)
            .Map(dest => dest.UserName, dest => dest.Name)
            .Map(dest => dest.Fingerprint, dest => dest.VisitorId);
        TypeAdapterConfig<ApplicationUser, UserResponse>.NewConfig()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Email, src => src.Email)
            .Map(dest => dest.Name, src => src.UserName);
        TypeAdapterConfig<PostCreateRequest, Post>.NewConfig()
            .Map(dest => dest.Title, src => src.Title)
            .Map(dest => dest.Description, src => src.Description)
            .Map(dest => dest.CurrentPrice, src => src.CurrentPrice)
            .Map(dest => dest.Latitude, src => src.Latitude)
            .Map(dest => dest.Longitude, src => src.Longitude)
            .Map(dest => dest.MaxPeople, src => src.MaxPeople)
            .Map(dest => dest.CreatedAt, src => src.CreatedAt);
        TypeAdapterConfig<PostUpdateRequest, Post>.NewConfig()
            .Map(dest => dest.Title, src => src.Title)
            .Map(dest => dest.Description, src => src.Description)
            .Map(dest => dest.CurrentPrice, src => src.CurrentPrice)
            .Map(dest => dest.Latitude, src => src.Latitude)
            .Map(dest => dest.Longitude, src => src.Longitude)
            .Map(dest => dest.MaxPeople, src => src.MaxPeople)
            .IgnoreNullValues(true);
        TypeAdapterConfig<Post, PostResponse>.NewConfig()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Title, src => src.Title)
            .Map(dest => dest.Description, src => src.Description)
            .Map(dest => dest.CurrentPrice, src => src.CurrentPrice)
            .Map(dest => dest.Latitude, src => src.Latitude)
            .Map(dest => dest.Longitude, src => src.Longitude)
            .Map(dest => dest.MaxPeople, src => src.MaxPeople)
            .Map(dest => dest.Creator, src => src.Creator.Adapt<UserResponse>())
            .Map(dest => dest.Members, src => src.Members.Adapt<List<UserResponse>>())
            .Map(dest => dest.UpdatedAt, src => src.UpdatedAt)
            .Map(dest => dest.CreatedAt, src => src.CreatedAt);
        TypeAdapterConfig<MessageCreateRequest, Message>.NewConfig()
            .Map(dest => dest.Content, src => src.Content)
            .Map(dest => dest.SentAt, src => src.SentAt);
        TypeAdapterConfig<Message, MessageResponse>.NewConfig()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Content, src => src.Content)
            .Map(dest => dest.SentAt, src => src.SentAt)
            .Map(dest => dest.Sender, src => src.Sender.Adapt<UserResponse>())
            .Map(dest => dest.UpdatedAt, src => src.UpdatedAt)
            .Map(dest => dest.Post, src => src.Post.Adapt<PostResponse>());
        TypeAdapterConfig<Notification, NotificationResponse>.NewConfig()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Title, src => src.Title)
            .Map(dest => dest.Description, src => src.Description)
            .Map(dest => dest.CreatedAt, src => src.CreatedAt)
            .Map(dest => dest.IsRead, src => src.IsRead)
            .Map(dest => dest.User, src => src.User.Adapt<UserResponse>());
        TypeAdapterConfig<PostNotification, PostNotificationResponse>.NewConfig()
            .Inherits<Notification, NotificationResponse>()
            .Map(dest => dest.Post, src => src.Post.Adapt<PostResponse>());
        TypeAdapterConfig<ApplicationUser, ProfileResponse>.NewConfig()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Email, src => src.Email)
            .Map(dest => dest.Username, src => src.UserName)
            .Map(dest => dest.RegisteredAt, src => src.RegistrationDate);
        TypeAdapterConfig<Post, PostCreatedNotification>.NewConfig()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Title, src => src.Title)
            .Map(dest => dest.Description, src => src.Description)
            .Map(dest => dest.CreatedAt, src => src.CreatedAt)
            .Map(dest => dest.MaxPeople, src => src.MaxPeople);
    }
}