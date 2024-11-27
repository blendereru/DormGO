using IdentityApiAuth.DTOs;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace IdentityApiAuth.Hubs;

public class PostHub : Hub
{
    public override Task OnConnectedAsync()
    {
        Log.Information("Client connected: {0}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public async Task NotifyPostCreated(string userName, PostDto post)
    {
        await Clients.All.SendAsync("PostCreated", userName, post);
    }
}