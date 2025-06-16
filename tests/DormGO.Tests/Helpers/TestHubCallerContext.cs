using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DormGO.Tests.Helpers;

public class TestHubCallerContext : HubCallerContext
{
    private readonly HttpContext _httpContext;
    private readonly FeatureCollection _features = new();

    public TestHubCallerContext(string? userId, string? connectionId,HttpContext httpContext)
    {
        ConnectionId = connectionId ?? Guid.NewGuid().ToString();
        _httpContext = httpContext;
        _features.Set<IHttpContextFeature>(new HttpContextFeature { HttpContext = _httpContext });
        if (string.IsNullOrWhiteSpace(userId))
        {
            User = new ClaimsPrincipal(new ClaimsIdentity());
        }
        else
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId)
            }));
        }
        UserIdentifier = userId;
    }

    public override string ConnectionId { get; }
    public override string? UserIdentifier { get; }

    public override ClaimsPrincipal User { get; }
    public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
    public override IFeatureCollection Features => _features;
    public override CancellationToken ConnectionAborted => CancellationToken.None;
    public override void Abort() => Aborted = true;

    public bool Aborted { get; private set; }
}


