using System.ComponentModel;

namespace DormGO.DTOs.RequestDTO;

public class NotificationUpdateRequest
{
    [Description("Mark the notification as read.")]
    public bool? IsRead { get; set; }
}