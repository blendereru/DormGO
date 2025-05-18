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
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                    Detail = "The email claim is missing from the token.",
                    Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}"
                });
                return;
            }

            var user = await _userManager.FindByEmailAsync(emailClaim);
            if (user == null)
            {
                _logger.LogWarning("User not found. Controller: {Controller}, Action: {Action}", controllerName, actionName);
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "User Not Found",
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                    Detail = "The user associated with the provided email was not found.",
                    Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}"
                });
                return;
            }

            if (!user.EmailConfirmed)
            {
                _logger.LogWarning("Email not confirmed for user: {UserId}. Controller: {Controller}, Action: {Action}", user.Id, controllerName, actionName);
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Email Not Confirmed",
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                    Detail = "Email address has not been confirmed.",
                    Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}"
                });
                return;
            }
            context.HttpContext.Items[HttpContextItemKeys.UserItemKey] = user;
        }
        await next();
    }
}