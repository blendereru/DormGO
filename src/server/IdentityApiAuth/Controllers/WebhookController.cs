using IdentityApiAuth.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace IdentityApiAuth.Controllers;
[ApiController]
[Route("/api/webhook")]
public class WebhookController : Controller
{
    private readonly ApplicationContext _db;

    public WebhookController(ApplicationContext db)
    {
        _db = db;
    }
    //ToDo: end up
    [HttpPost("{eventType}")]
    public async Task<IActionResult> ReceiveEvent(string eventType, [FromBody] JObject payload)
    {
        if (string.IsNullOrEmpty(eventType))
        {
            return BadRequest("The event type is required");
        }
    }
}