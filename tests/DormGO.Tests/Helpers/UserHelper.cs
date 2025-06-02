using DormGO.Models;

namespace DormGO.Tests.Helpers;

public static class UserHelper
{
    public static ApplicationUser CreateUser()
    {
        return new ApplicationUser
        {
            Email = "your@example.com",
            UserName = "blendereru",
            EmailConfirmed = true,
            Fingerprint = "sample_visitor_id"
        };
    }
}