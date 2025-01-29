using DormGO.DTOs;
using DormGO.Models;
using Mapster;

namespace DormGO.Mappings;
public static class MapsterConfig
{
    public static void Configure()
    {
        TypeAdapterConfig<Post, PostDto>.NewConfig()
            .Map(dest => dest.PostId, src => src.Id)
            .Map(dest => dest.Description, src => src.Description)
            .Map(dest => dest.CurrentPrice, src => src.CurrentPrice)
            .Map(dest => dest.Latitude, src => src.Latitude)
            .Map(dest => dest.Longitude, src => src.Longitude)
            .Map(dest => dest.CreatedAt, src => src.CreatedAt)
            .Map(dest => dest.MaxPeople, src => src.MaxPeople)
            .Map(dest => dest.Creator, src => src.Creator.Adapt<MemberDto>())
            .Map(dest => dest.Members, src => src.Members.Adapt<List<MemberDto>>());
        TypeAdapterConfig<Post, UpdatePostDto>.NewConfig()
            .Map(dest => dest.Description, src => src.Description)
            .Map(dest => dest.CurrentPrice, src => src.CurrentPrice)
            .Map(dest => dest.Latitude, src => src.Latitude)
            .Map(dest => dest.Longitude, src => src.Longitude)
            .Map(dest => dest.MaxPeople, src => src.MaxPeople);
        TypeAdapterConfig<UserDto, ApplicationUser>.NewConfig()
            .Map(dest => dest.Email, src => src.Email)
            .Map(dest => dest.UserName, src => src.Email)
            .Map(dest => dest.Fingerprint, src => src.VisitorId);
        TypeAdapterConfig<ApplicationUser, MemberDto>.NewConfig()
            .Map(dest => dest.Email, src => src.Email)
            .Map(dest => dest.Name, src => src.UserName);
        TypeAdapterConfig<Message, MessageDto>.NewConfig()
            .Map(dest => dest.MessageId, src => src.Id)
            .Map(dest => dest.Sender, src => src.Sender)
            .Map(dest => dest.Content, src => src.Content)
            .Map(dest => dest.SentAt, src => src.SentAt);
    }
}