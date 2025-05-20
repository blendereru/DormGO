using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Models;
using Mapster;

namespace DormGO.Mappings;
public static class MapsterConfig
{
    public static void Configure()
    {
        TypeAdapterConfig<UserRequestDto, ApplicationUser>.NewConfig()
            .Map(dest => dest.Email, src => src.Email)
            .Map(dest => dest.UserName, src => src.Name ?? src.Email)
            .Map(dest => dest.Fingerprint, src => src.VisitorId);
        TypeAdapterConfig<ApplicationUser, UserResponseDto>.NewConfig()
            .Map(dest => dest.Email, src => src.Email)
            .Map(dest => dest.Name, src => src.UserName);
        TypeAdapterConfig<PostRequestDto, Post>.NewConfig()
            .Map(dest => dest.Title, src => src.Title)
            .Map(dest => dest.Description, src => src.Description)
            .Map(dest => dest.CurrentPrice, src => src.CurrentPrice)
            .Map(dest => dest.Latitude, src => src.Latitude)
            .Map(dest => dest.Longitude, src => src.Longitude)
            .Map(dest => dest.MaxPeople, src => src.MaxPeople);
        TypeAdapterConfig<Post, PostResponseDto>.NewConfig()
            .Map(dest => dest.PostId, src => src.Id)
            .Map(dest => dest.Title, src => src.Title)
            .Map(dest => dest.Description, src => src.Description)
            .Map(dest => dest.CurrentPrice, src => src.CurrentPrice)
            .Map(dest => dest.Latitude, src => src.Latitude)
            .Map(dest => dest.Longitude, src => src.Longitude)
            .Map(dest => dest.MaxPeople, src => src.MaxPeople)
            .Map(dest => dest.Creator, src => src.Creator.Adapt<UserResponseDto>())
            .Map(dest => dest.Members, src => src.Members.Adapt<List<UserResponseDto>>())
            .Map(dest => dest.UpdatedAt, src => src.UpdatedAt);
        TypeAdapterConfig<MessageRequestDto, Message>.NewConfig()
            .Map(dest => dest.Content, src => src.Content);
        TypeAdapterConfig<Message, MessageResponseDto>.NewConfig()
            .Map(dest => dest.MessageId, src => src.Id)
            .Map(dest => dest.Content, src => src.Content)
            .Map(dest => dest.SentAt, src => src.SentAt)
            .Map(dest => dest.Sender, src => src.Sender.Adapt<UserResponseDto>())
            .Map(dest => dest.UpdatedAt, src => src.UpdatedAt)
            .Map(dest => dest.Post, src => src.Post.Adapt<PostResponseDto>());
        TypeAdapterConfig<NotificationRequestDto, PostNotification>.NewConfig()
            .Map(dest => dest.Message, src => src.Message);
        TypeAdapterConfig<PostNotification, NotificationResponseDto>.NewConfig()
            .Map(dest => dest.NotificationId, src => src.Id)
            .Map(dest => dest.Message, src => src.Message)
            .Map(dest => dest.CreatedAt, src => src.CreatedAt)
            .Map(dest => dest.Post, src => src.Post.Adapt<PostResponseDto>())
            .Map(dest => dest.IsRead, src => src.IsRead)
            .Map(dest => dest.User, src => src.User.Adapt<UserResponseDto>());
        TypeAdapterConfig<ApplicationUser, ProfileResponseDto>.NewConfig()
            .Map(dest => dest.UserId, src => src.Id)
            .Map(dest => dest.Email, src => src.Email)
            .Map(dest => dest.Username, src => src.UserName)
            .Map(dest => dest.RegisteredAt, src => src.RegistrationDate);
    }
}