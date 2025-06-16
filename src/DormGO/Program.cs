using System.Text.Json.Serialization;
using DormGO;
using DormGO.Data;
using DormGO.Filters;
using DormGO.Hubs;
using DormGO.Mappings;
using DormGO.Models;
using DormGO.Services;
using DormGO.Services.HubNotifications;
using Mapster;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddIdentityCore<ApplicationUser>(opts =>
    {
        opts.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationContext>()
    .AddDefaultTokenProviders();
builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig.ReadFrom.Configuration(context.Configuration);
});
builder.Configuration.AddEnvironmentVariables();
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.AddSwaggerGen(c =>
{
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});
builder.Services.AddAuthentication(opts =>
    {
        opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opts.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opts =>
    {
        var authOptions = builder.Configuration.GetSection("JwtSettings").Get<AuthOptions>()!;
        opts.MapInboundClaims = false;
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = authOptions.Audience,
            ValidateIssuer = true,
            ValidIssuer = authOptions.Issuer,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = JwtRegisteredClaimNames.Name,
            IssuerSigningKey = authOptions.GetSymmetricSecurityKey(),
            ClockSkew = TimeSpan.Zero
        };

        opts.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = _ =>
            {
                Console.WriteLine("Token successfully validated.");
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddDbContext<ApplicationContext>(opts =>
{
    opts.UseSqlServer(builder.Configuration.GetConnectionString("IdentityConnection"));
});
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance =
            $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
        context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
        var activity = context.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity;
        context.ProblemDetails.Extensions.TryAdd("traceId", activity?.Id);
    };
});
builder.Services.AddSignalR();
builder.Services.AddMapster();
MapsterConfig.Configure();
builder.Services.AddScoped<IEmailSender<ApplicationUser>, EmailSender>();
builder.Services.AddSingleton<IInputSanitizer, InputSanitizer>();
builder.Services.AddScoped<IUserHubNotificationService, UserHubNotificationService>();
builder.Services.AddScoped<IPostHubNotificationService, PostHubNotificationService>();
builder.Services.AddScoped<IChatHubNotificationService, ChatHubNotificationService>();
builder.Services.AddScoped<ValidateUserEmailFilter>();
builder.Services.AddScoped(typeof(INotificationHubNotificationService<>), typeof(NotificationHubNotificationService<>));
builder.Services.AddScoped<ITokensProvider, TokensProvider>();
builder.Services.AddScoped<IRazorViewToStringRenderer, RazorViewToStringRenderer>();
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DormGO API v1");
});
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<UserHub>("/api/userhub");
app.MapHub<PostHub>("/api/posthub");
app.MapHub<ChatHub>("/api/chathub");
app.MapHub<NotificationHub>("/api/notificationhub");
app.Run();
public partial class Program { }