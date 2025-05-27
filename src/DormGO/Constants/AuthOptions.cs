using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DormGO.Constants;

public static class AuthOptions
{
    public const string ISSUER = "MyAuthServer";
    public const string AUDIENCE = "MyAuthClient";
    public const int LIFETIME = 30; // this value gets changed every time i test
    private const string KEY = "mysupersecret_secretsecretsecretkey!123";
    public static SymmetricSecurityKey GetSymmetricSecurityKey() => 
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(KEY));
}