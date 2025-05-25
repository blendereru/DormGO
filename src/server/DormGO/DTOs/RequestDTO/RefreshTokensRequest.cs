using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class RefreshTokensRequest
{
    [Description("The access token (JWT) previously issued to the user. It is used to identify the user and verify token validity.")]
    [Required]
    public string AccessToken { get; set; }
    [Description("The refresh token issued alongside the access token. It is used to obtain a new access token without re-authenticating.")]
    [Required]
    public string RefreshToken { get; set; }
    [Description("A unique identifier representing the user's browser or device. This is used to associate the session with a specific environment.")]
    [Required]
    public string VisitorId { get; set; }
}