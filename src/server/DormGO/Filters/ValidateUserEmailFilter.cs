using System.Security.Claims;
using DormGO.Constants;
using DormGO.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Serilog;

namespace DormGO.Filters;

public class ValidateUserEmailFilter : IAsyncActionFilter
{
    private readonly UserManager<ApplicationUser> _userManager;
    public ValidateUserEmailFilter(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var actionDescriptor = context.ActionDescriptor as ControllerActionDescriptor;
        var actionName = actionDescriptor?.ActionName;
        var emailClaim = context.HttpContext.User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(emailClaim))
        {
            Log.Warning("Unauthorized access attempt. Email claim missing. Action; {Action}", actionName);
            context.Result = new JsonResult(new { Message = "The email claim is missing from the token."})
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        var user = await _userManager.FindByEmailAsync(emailClaim);
        if (user == null)
        {
            Log.Warning("User not found with email: {Email}. Action: {Action}", emailClaim, actionName);
            context.Result = new JsonResult(new { Message = "The user with the provided email is not found." })
            {
                StatusCode = StatusCodes.Status404NotFound
            };
            return;
        }
        if (!user.EmailConfirmed)
        {
            Log.Warning("Email not confirmed for user: {Email}. Action: {Action}", emailClaim, actionName);
            context.Result = new JsonResult(new { Message = "Email not confirmed." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        context.HttpContext.Items[HttpContextItemKeys.UserItemKey] = user;
        await next();
    }
}