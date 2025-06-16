using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DormGO;

public class AuthOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int Lifetime { get; set; }
    public string Key { get; set; } = string.Empty;

    public SymmetricSecurityKey GetSymmetricSecurityKey() => 
        new(Encoding.UTF8.GetBytes(Key));
}