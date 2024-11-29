using IdentityApiAuth.Models;
using Microsoft.AspNetCore.SignalR;

namespace IdentityApiAuth.Hubs;

public class UserHub : Hub
{
    private readonly ApplicationContext _db;
    public UserHub(ApplicationContext db)
    {
        _db = db;
    }

    public async Task NotifyEmailConfirmed(string userName, TokenRequest request)
    {
        await Clients.Caller.SendAsync("EmailConfirmed", userName, request);
    }
}