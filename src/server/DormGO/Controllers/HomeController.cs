using System.Net;
using System.Security.Claims;
using DormGO.Contracts;
using DormGO.DTOs;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace DormGO.Controllers;

[Authorize]
[ApiController]
[Route("api/post")]
public class HomeController : ControllerBase
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }
    [HttpPost("create")]
    public async Task<IActionResult> CreatePost([FromBody] PostDto postDto, [FromServices] IRequestClient<CreatePost> client)
    {
        var creatorEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(creatorEmail))
        {
            _logger.LogWarning("Missing email claim.");
            return BadRequest(new { Message = "The user's email was not found from jwt token" });
        }
        var response = await client.GetResponse<OperationResponse<PostDto>>(new()
        {
            Post = postDto,
            CreatorEmail = creatorEmail
        });
        var responseMessage = response.Message;
        var reply = new
        {
            responseMessage.Message,
            responseMessage.Data
        };
        return StatusCode((int)responseMessage.StatusCode, reply);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchPosts([FromQuery] SearchPostDto searchDto, [FromServices] IRequestClient<SearchPosts> client)
    {
        var userEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(userEmail))
        {
            _logger.LogWarning("Email claim not found in JWT token");
            return Unauthorized(new { Message = "The email claim is not found" });
        }

        var response = await client.GetResponse<OperationResponse<List<PostDto>>>(new()
        {
            Filter = searchDto,
            UserEmail = userEmail
        });
        var responseMessage = response.Message;
        var reply = new
        {
            responseMessage.Message,
            responseMessage.Data
        };
        return StatusCode((int)responseMessage.StatusCode, reply);
    }
    [HttpGet("read")]
    public async Task<IActionResult> ReadPosts(bool joined, [FromServices] IRequestClient<ReadPost> client)
    {
        var userEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(userEmail))
        {
            _logger.LogWarning("Email claim not found.");
            return Unauthorized(new { Message = "The email claim is not found" });
        }
        var request = new ReadPost
        {
            Joined = joined,
            UserEmail = userEmail
        };
        try
        {
            var response = await client.GetResponse<OperationResponse<PostDto>, OperationResponse<object>>(request);
            if (response.Is(out Response<OperationResponse<PostDto>>? postDtoResponse))
            {
                var responseMessage = postDtoResponse.Message;
                var reply = new
                {
                    responseMessage.Message,
                    responseMessage.Data
                };
                return StatusCode((int)responseMessage.StatusCode, reply);
            }
            if (response.Is(out Response<OperationResponse<object>>? objectResponse))
            {
                var responseMessage = objectResponse.Message;
                var reply = new
                {
                    responseMessage.Message,
                    responseMessage.Data
                };
                return StatusCode((int)responseMessage.StatusCode, reply);
            }
            _logger.LogError("Unexpected response type received.");
            return StatusCode((int)HttpStatusCode.InternalServerError, new { Message = "Unexpected response type." });
        }
        catch (RequestTimeoutException ex)
        {
            _logger.LogError(ex, "Request to ReadPostConsumer timed out.");
            return StatusCode((int)HttpStatusCode.RequestTimeout, new { Message = "The request timed out." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            return StatusCode((int)HttpStatusCode.InternalServerError, new { Message = "An error occurred while processing your request." });
        }
    }

    [HttpGet("read/{id}")]
    public async Task<IActionResult> ReadPost(string id, [FromServices] IRequestClient<ReadPost> client)
    {
        var userEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(userEmail))
        {
            _logger.LogWarning("Missing email claim.");
            return Unauthorized(new { Message = "The email claim is not found" });
        }
        var response = await client.GetResponse<OperationResponse<PostDto>>(new()
        {
            PostId = id,
            UserEmail = userEmail
        });
        var responseMessage = response.Message;
        var reply = new
        {
            responseMessage.Message,
            responseMessage.Data
        };
        return StatusCode((int)responseMessage.StatusCode, reply);
    }

    [HttpPut("join/{id}")]
    public async Task<IActionResult> JoinPost(string id, [FromServices] IRequestClient<JoinPost> client)
    {
        var userEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(userEmail))
        {
            _logger.LogWarning("Missing email claim.");
            return Unauthorized(new { Message = "The email claim is not found" });
        }
        var response = await client.GetResponse<OperationResponse<PostDto>>(new()
        {
            PostId = id,
            UserEmail = userEmail
        });
        var responseMessage = response.Message;
        var reply = new
        {
            responseMessage.Message,
            responseMessage.Data
        };
        return StatusCode((int)responseMessage.StatusCode, reply);
    }

    [HttpPut("{id}/transfer-ownership")]
    public async Task<IActionResult> TransferPostOwnership(string id, [FromBody] MemberDto memberDto, [FromServices] IRequestClient<TransferOwnership> client)
    {
        var userEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(userEmail))
        {
            _logger.LogWarning("Missing email claim.");
            return Unauthorized(new { Message = "The email claim is not found" });
        }

        var response = await client.GetResponse<OperationResponse<PostDto>>(new()
        {
            PostId = id,
            NewOwner = memberDto,
            UserEmail = userEmail
        });
        var responseMessage = response.Message;
        var reply = new
        {
            responseMessage.Message,
            responseMessage.Data
        };
        return StatusCode((int)responseMessage.StatusCode, reply);
    }
    [HttpPut("update/{id}")]
    public async Task<IActionResult> UpdatePost(string id, [FromBody] UpdatePostDto postDto, [FromServices] IRequestClient<UpdatePost> client)
    {
        var userEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(userEmail))
        {
            _logger.LogWarning("Unauthorized attempt to update post. User's email is missing in the JWT token.");
            return Unauthorized(new { Message = "User's email not found in the JWT token." });
        }
        var response = await client.GetResponse<OperationResponse<PostDto>>(new
        {
            id,
            postDto,
            userEmail
        });
        var responseMessage = response.Message;
        var reply = new
        {
            responseMessage.Message,
            responseMessage.Data
        };
        return StatusCode((int)responseMessage.StatusCode, reply);
    }
    [HttpDelete("unjoin/{id}")]
    public async Task<IActionResult> UnjoinPost(string id, [FromServices] IRequestClient<UnjoinPost> client)
    {
        var userEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(userEmail))
        {
            _logger.LogWarning("Unauthorized attempt to unjoin post. User's email is missing in the JWT token.");
            return Unauthorized(new { Message = "User's email not found in the JWT token." });
        }
        var response = await client.GetResponse<OperationResponse<PostDto>>(new()
        {
            PostId = id,
            UserEmail = userEmail
        });
        var responseMessage = response.Message;
        var reply = new
        {
            responseMessage.Message,
            responseMessage.Data
        };
        return StatusCode((int)responseMessage.StatusCode, reply);
    }

    [HttpDelete("delete/{id}")]
    public async Task<IActionResult> RemovePost(string id, [FromServices] IRequestClient<DeletePost> client)
    {
        var userEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(userEmail))
        {
            Log.Warning("RemovePost: Unauthorized attempt to delete post with ID {PostId}. User's email is missing in the JWT token.", id);
            return Unauthorized(new { Message = "User's email not found in the JWT token." });
        }
        var response = await client.GetResponse<OperationResponse<PostDto>>(new()
        {
            PostId = id,
            UserEmail = userEmail
        });
        var responseMessage = response.Message;
        var reply = new
        {
            responseMessage.Message,
            responseMessage.Data
        };
        return StatusCode((int)responseMessage.StatusCode, reply);
    }
}