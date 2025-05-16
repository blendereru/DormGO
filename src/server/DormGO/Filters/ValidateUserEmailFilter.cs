using System.Security.Claims;
using DormGO.Constants;
using DormGO.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DormGO.Filters;

public class ValidateUserEmailFilter : IAsyncActionFilter
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ValidateUserEmailFilter> _logger;
    public ValidateUserEmailFilter(UserManager<ApplicationUser> userManager, ILogger<ValidateUserEmailFilter> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor descriptor)
        {
            var controllerName = descriptor.ControllerName;
            var actionName = descriptor.ActionName;

            var emailClaim = context.HttpContext.User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(emailClaim))
            {
                _logger.LogWarning("Unauthorized access attempt. Email claim missing. Controller: {Controller}, Action: {Action}", controllerName, actionName);
                context.Result = new JsonResult(new { Message = "The email claim is missing from the token." })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
                return;
            }

            var user = await _userManager.FindByEmailAsync(emailClaim);
            if (user == null)
            {
                _logger.LogWarning("User not found. Controller: {Controller}, Action: {Action}", controllerName, actionName);
                context.Result = new JsonResult(new { Message = "The user is not found." })
                {
                    StatusCode = StatusCodes.Status404NotFound
                };
                return;
            }

            if (!user.EmailConfirmed)
            {
                _logger.LogWarning("Email not confirmed for user: {UserId}. Controller: {Controller}, Action: {Action}", user.Id, controllerName, actionName);
                context.Result = new JsonResult(new { Message = "Email not confirmed." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            context.HttpContext.Items[HttpContextItemKeys.UserItemKey] = user;
        }

        await next();
    }
}