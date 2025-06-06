using DormGO.Models;

namespace DormGO.Tests.Helpers;

public static class MessageHelper
{
    public static Message CreateMessage(ApplicationUser sender, Post post)
    {
        return new Message
        {
            Id = "test_message_id",
            Content = "content",
            SenderId = sender.Id,
            PostId = post.Id,
            SentAt = DateTime.UtcNow
        };
    }
}