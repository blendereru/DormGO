using DormGO.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace DormGO.Tests.Helpers;

public static class HubConnectionHelper
{
    public static async Task<HubConnection> ConnectUserAsync(ApplicationUser user, Uri baseUri)
    {
        var jwtToken = TokenHelper.GenerateJwt(user.Id, user.Email, user.EmailConfirmed.ToString(), null);
        var hubUri = new Uri(baseUri, "api/posthub");
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUri, conf =>
            {
                conf.Headers.Add("Authorization", $"Bearer {jwtToken}");
            })
            .Build();
        await connection.StartAsync();
        return connection;
    }
}