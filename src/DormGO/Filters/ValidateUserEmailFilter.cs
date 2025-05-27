using System.Security.Claims;
using DormGO.Constants;
using DormGO.Models;
using Microsoft.AspNetCore.Http.Features;
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
                context.Result = ProblemResult(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    "Unauthorized",
                    "https://tools.ietf.org/html/rfc7235#section-3.1",
                    "The email claim is missing from the token."
                );
                return;
            }
            var user = await _userManager.FindByEmailAsync(emailClaim);
            if (user == null)
            {
                _logger.LogWarning("User not found. Controller: {Controller}, Action: {Action}", controllerName, actionName);
                context.Result = ProblemResult(
                    context.HttpContext,
                    StatusCodes.Status404NotFound,
                    "User Not Found",
                    "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                    "The user associated with the provided email was not found."
                );
                return;
            }

            if (!user.EmailConfirmed)
            {
                _logger.LogWarning("Email not confirmed for user: {UserId}. Controller: {Controller}, Action: {Action}", user.Id, controllerName, actionName);
                context.Result = ProblemResult(
                    context.HttpContext,
                    StatusCodes.Status403Forbidden,
                    "Email Not Confirmed",
                    "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                    "Email address has not been confirmed."
                );
                return;
            }
            context.HttpContext.Items[HttpContextItemKeys.UserItemKey] = user;
        }
        await next();
    }
    private static ObjectResult ProblemResult(HttpContext httpContext, int status, string title, string type, string detail)
    {
        var pd = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };
        pd.Extensions.TryAdd("requestId", httpContext.TraceIdentifier);
        var activity = httpContext.Features.Get<IHttpActivityFeature>()?.Activity;
        pd.Extensions.TryAdd("traceId", activity?.Id);
        return new ObjectResult(pd) { StatusCode = status };
    }
}