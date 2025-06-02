using DormGO.Constants;
using DormGO.Models;
using Microsoft.AspNetCore.Http;

namespace DormGO.Tests.Helpers;

public static class HttpContextItemsHelper
{
    public static void SetHttpContextItems(HttpContext httpContext, ApplicationUser user)
        => httpContext.Items.Add(HttpContextItemKeys.UserItemKey, user);
}