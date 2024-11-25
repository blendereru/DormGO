using IdentityApiAuth.DTOs;
using Mapster;

namespace IdentityApiAuth.Models;
public static class MapsterConfig
{
    public static void Configure()
    {
        TypeAdapterConfig<Post, PostDto>.NewConfig()
            .Map(dest => dest.Description, src => src.Description)
            .Map(dest => dest.CurrentPrice, src => src.CurrentPrice)
            .Map(dest => dest.Latitude, src => src.Latitude)
            .Map(dest => dest.Longitude, src => src.Longitude)
            .Map(dest => dest.CreatedAt, src => src.CreatedAt)
            .Map(dest => dest.MaxPeople, src => src.MaxPeople)
            .Map(dest => dest.Members, src => src.Members.Adapt<List<MemberDto>>());
        TypeAdapterConfig<UserDto, ApplicationUser>.NewConfig()
            .Map(dest => dest.Email, src => src.Email)
            .Map(dest => dest.UserName, src => src.Email);
        TypeAdapterConfig<ApplicationUser, MemberDto>.NewConfig()
            .Map(dest => dest.Email, src => src.Email)
            .Map(dest => dest.Name, src => src.UserName);
    }
}