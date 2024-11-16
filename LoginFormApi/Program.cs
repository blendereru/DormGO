using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LoginFormApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters()
        {
            ValidateIssuer = true,
            ValidIssuer = AuthOptions.ISSUER,
            ValidateAudience = true,
            ValidAudience = AuthOptions.AUDIENCE,
            ValidateLifetime = true,
            IssuerSigningKey = AuthOptions.GetSymmetricSecurityKey(),
            ValidateIssuerSigningKey = true
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddDbContext<ApplicationContext>(opts =>
{
    opts.UseSqlServer(builder.Configuration.GetConnectionString("LoginService"));
});
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapPost("/api/signin", async (User model, ApplicationContext db) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == model.Email && u.Password == model.Password);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var token = GenerateJwtToken(user);

    return Results.Ok(new { Token = token });
});
app.MapPost("/api/signup", async (User model, ApplicationContext db) =>
{
    if (!model.HasValidPassword())
    {
        return Results.BadRequest("The password isn't secure");
    }

    if (!model.Email.EndsWith("@kbtu.kz"))
    {
        return Results.BadRequest("The email should end with `@kbtu.kz`");
    }
    var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
    if (existingUser != null)
    {
        return Results.BadRequest("User already exists.");
    }
    var newUser = new User
    {
        Email = model.Email,
        Password = model.Password
    };
    await db.Users.AddAsync(newUser);
    await db.SaveChangesAsync();
    var token = GenerateJwtToken(newUser);

    // Return the token along with a success message
    return Results.Ok(new { Message = "User registered successfully", Token = token });
});
app.MapGet("/api/protected", (ClaimsPrincipal user) =>
{
    var email = user.Identity?.Name;
    return Results.Ok($"Hello, {email}! This is a protected endpoint.");
}).RequireAuthorization();

app.Run();
string GenerateJwtToken(User user)
{
    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.Email),
        new Claim(ClaimTypes.NameIdentifier, user.Email),
        new Claim(ClaimTypes.Role, "User")
    };

    var jwt = new JwtSecurityToken(
        issuer: AuthOptions.ISSUER,
        audience: AuthOptions.AUDIENCE,
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(AuthOptions.LIFETIME),
        signingCredentials: new SigningCredentials(AuthOptions.GetSymmetricSecurityKey(), SecurityAlgorithms.HmacSha256)
    );

    return new JwtSecurityTokenHandler().WriteToken(jwt);
}
public partial class Program {}