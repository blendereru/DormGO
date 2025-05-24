using System.Security.Claims;
using DormGO.Models;

namespace DormGO.Services;

public interface ITokensProvider
{
    string GenerateAccessToken(ApplicationUser user);
    string GenerateRefreshToken();
    Task<ClaimsPrincipal?> GetPrincipalFromExpiredTokenAsync(string token);
}