using System.Net;
using DormGO.Contracts;
using DormGO.Data;
using DormGO.DTOs;
using DormGO.Models;
using Mapster;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DormGO.Components;

public class SearchPostsConsumer : IConsumer<SearchPosts>
{
    private readonly ApplicationContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<SearchPostsConsumer> _logger;

    public SearchPostsConsumer(ApplicationContext db, UserManager<ApplicationUser> userManager,
        ILogger<SearchPostsConsumer> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }
    public async Task Consume(ConsumeContext<SearchPosts> context)
    {
        var filter = context.Message.Filter;
        var userEmail = context.Message.UserEmail;
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["UserEmail"] = userEmail,
                   ["CorrelationId"] = context.CorrelationId?.ToString() ?? Guid.NewGuid().ToString()
               }))
        {
            try
            {
                _logger.LogInformation("Starting search post information: {@Filter}", filter);
                var query = _db.Posts.AsQueryable();
                if (!string.IsNullOrEmpty(filter.SearchText))
                {
                    _logger.LogDebug("Applying text filter: {SearchText}", filter.SearchText);
                    var searchTerm = filter.SearchText.ToLower();
                    query = query.Where(p => p.Description.ToLower().Contains(searchTerm));
                }

                if (filter.StartDate.HasValue)
                {
                    _logger.LogDebug("Applying start date filter: {StartDate}", filter.StartDate.Value);
                    query = query.Where(p => p.CreatedAt >= filter.StartDate.Value);
                }

                if (filter.EndDate.HasValue)
                {
                    _logger.LogDebug("Applying end date filter: {EndDate}", filter.EndDate.Value);
                    query = query.Where(p => p.CreatedAt <= filter.EndDate.Value);
                }
                if (filter.Members.Count > 0)
                {
                    _logger.LogDebug("Processing member filter for {Count} emails", filter.Members.Count);
                    var memberEmails = filter.Members.Select(m => m.Email).ToList();
                    var users = await _db.Users
                        .Where(u => memberEmails.Contains(u.Email))
                        .ToListAsync();
                    if (!users.Any())
                    {
                        _logger.LogInformation("No users found for provided member emails");
                        await context.RespondAsync<OperationResponse<List<PostDto>>>(new()
                        {
                            Success = true,
                            StatusCode = HttpStatusCode.Found,
                            Message = "No user found for current search term",
                            Data = new List<PostDto>()
                        });
                    }

                    _logger.LogDebug("Found {UserCount} matching users in database", users.Count);
                    var userIds = users.Select(u => u.Id).ToList();
                    foreach (var userId in userIds)
                    {
                        query = query.Where(p => p.Members.Any(m => m.Id == userId));
                    }
                }

                if (filter.MaxPeople.HasValue)
                {
                    _logger.LogDebug("Applying max people filter: {MaxPeople}", filter.MaxPeople.Value);
                    query = query.Where(p => p.MaxPeople <= filter.MaxPeople.Value);
                }

                if (filter.OnlyAvailable.HasValue && filter.OnlyAvailable.Value)
                {
                    _logger.LogDebug("Applying availability filter (only non-full posts)");
                    query = query.Where(p => p.Members.Count < p.MaxPeople);
                }

                var posts = await query
                    .Include(p => p.Creator)
                    .Include(p => p.Members)
                    .ProjectToType<PostDto>()
                    .ToListAsync();
                _logger.LogInformation("Search completed. Found {PostCount} results", posts.Count);
                await context.RespondAsync<OperationResponse<List<PostDto>>>(new()
                {
                    Success = true,
                    StatusCode = HttpStatusCode.Found,
                    Message = $"{posts.Count} posts found",
                    Data = posts
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error occurred during post search process.");
                await context.RespondAsync<OperationResponse<PostDto>>(new()
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = "An unexpected error occurred."
                });
            }
        }
    }
}