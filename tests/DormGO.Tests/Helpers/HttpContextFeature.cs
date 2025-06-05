using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;

namespace DormGO.Tests.Helpers;

public class HttpContextFeature : IHttpContextFeature
{
    public HttpContext? HttpContext { get; set; }
}