using System.Security.Claims;
using DormGO;
using DormGO.Components;
using DormGO.Contracts;
using DormGO.Data;
using DormGO.Hubs;
using DormGO.Mappings;
using DormGO.Models;
using DormGO.Services;
using Mapster;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdentityCore<ApplicationUser>()
    .AddEntityFrameworkStores<ApplicationContext>()
    .AddDefaultTokenProviders();
builder.Host.UseSerilog();
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddControllers();
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<CreatePostConsumer>()
        .Endpoint(e => e.Name = "create-post");
    x.AddConsumer<UpdatePostConsumer>()
        .Endpoint(e => e.Name = "update-post");
    x.AddConsumer<JoinPostConsumer>()
        .Endpoint(e => e.Name = "join-post");
    x.AddConsumer<UnjoinPostConsumer>()
        .Endpoint(e => e.Name = "unjoin-post");
    x.AddConsumer<ReadPostConsumer>()
        .Endpoint(e => e.Name = "read-post");
    x.AddConsumer<DeletePostConsumer>()
        .Endpoint(e => e.Name = "delete-post");
    x.AddConsumer<TransferOwnershipConsumer>()
        .Endpoint(e => e.Name = "transfer-ownership");
    x.AddConsumer<SearchPostsConsumer>()
        .Endpoint(e => e.Name = "search-posts");
    x.AddRequestClient<CreatePost>(new Uri("exchange:create-post"));
    x.AddRequestClient<UpdatePost>(new Uri("exchange:update-post"));
    x.AddRequestClient<JoinPost>(new Uri("exchange:join-post"));
    x.AddRequestClient<UnjoinPost>(new Uri("exchange:unjoin-post"));
    x.AddRequestClient<ReadPost>(new Uri("exchange:read-post"));
    x.AddRequestClient<DeletePost>(new Uri("exchange:delete-post"));
    x.AddRequestClient<TransferOwnership>(new Uri("exchange:transfer-ownership"));
    x.AddRequestClient<SearchPosts>(new Uri("exchange:search-posts"));
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });
        cfg.ConfigureEndpoints(context);
    });
});
builder.Services.AddAuthentication(opts =>
    {
        opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opts.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = AuthOptions.AUDIENCE,
            ValidateIssuer = true,
            ValidIssuer = AuthOptions.ISSUER,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = ClaimTypes.Name,
            IssuerSigningKey = AuthOptions.GetSymmetricSecurityKey(),
            ClockSkew = TimeSpan.Zero
        };

        opts.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
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
builder.Services.AddSignalR();
builder.Services.AddMapster();
MapsterConfig.Configure();
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddScoped<INotificationService, PostNotificationService>();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
    dbContext.Database.Migrate();
}
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<UserHub>("/api/userhub");
app.MapHub<PostHub>("/api/posthub");
app.MapHub<ChatHub>("/api/chathub");
app.Run();
public partial class Program {}