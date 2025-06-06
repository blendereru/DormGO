using DormGO.Controllers;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Models;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DormGO.Tests.UnitTests;

public class ProfileControllerTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly ProfileController _controller;
    public ProfileControllerTests()
    {
        _userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        _controller = ControllerTestHelper.CreateProfileController(_userManagerMock.Object);
    }

    [Fact]
    public void GetMyProfile_ForUnauthorizedUser_ReturnsUnauthorizedResulWithProblemDetails()
    {
        // Act
        var result = _controller.GetMyProfile();
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }

    [Fact]
    public void GetMyProfile_ForAuthorizedUser_ReturnsOkResulWithProfileResponse()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = _controller.GetMyProfile();
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var response = Assert.IsType<ProfileResponse>(okResult.Value);
        Assert.NotNull(response);
        Assert.Equal(testUser.Id, response.Id);
    }

    [Fact]
    public async Task UpdateMyProfile_ForUnauthorizedUser_ReturnsUnauthorizedResulWithProblemDetails()
    {
        // Act
        var result = await _controller.UpdateMyProfile(new UserUpdateRequest());
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }

    [Fact]
    public async Task UpdateMyProfile_WhenUserNameUpdateRequestedAndValueNotChanged_ReturnsBadRequestResultWithProblemDetails()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var request = new UserUpdateRequest
        {
            NewUserName = testUser.UserName
        };
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.UpdateMyProfile(request);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
        Assert.Equal("No Update Performed", problemDetails.Title);
    }

    [Fact]
    public async Task UpdateMyProfile_WhenEmailUpdateRequestedAndValueNotChanged_ReturnsBadRequestResultWithProblemDetails()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var request = new UserUpdateRequest
        {
            NewEmail = testUser.Email
        };
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.UpdateMyProfile(request);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
        Assert.Equal("No Update Performed", problemDetails.Title);
    }

    [Fact]
    public async Task UpdateMyProfile_WhenPasswordUpdateRequestedWithConfirmNewPasswordNotProvided_ReturnsBadRequestResultWithValidationProblemDetails()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var request = new UserUpdateRequest
        {
            NewPassword = "test_new_password"
        };
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.UpdateMyProfile(request);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "Confirm new password field is required.";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Fact]
    public async Task UpdateMyProfile_WhenPasswordUpdateRequestedWithCurrentPasswordNotProvided_ReturnsBadRequestResultWithValidationProblemDetails()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var request = new UserUpdateRequest
        {
            NewPassword = "test_new_password",
            ConfirmNewPassword = "test_new_password"
        };
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.UpdateMyProfile(request);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "Current password field is required.";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Fact]
    public async Task UpdateMyProfile_WhenPasswordUpdateRequestedWithNewPasswordNotProvided_ReturnsBadRequestResultWithValidationProblemDetails()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var request = new UserUpdateRequest
        {
            CurrentPassword = "password",
            ConfirmNewPassword = "test_new_password"
        };
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.UpdateMyProfile(request);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "New password field is required.";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Fact]
    public async Task UpdateMyProfile_WhenPasswordUpdateRequestedWithEqualPasswordsValue_ReturnsBadRequestResultWithValidationProblemDetails()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var request = new UserUpdateRequest
        {
            CurrentPassword = "test_password",
            NewPassword = "test_password",
            ConfirmNewPassword = "test_password"
        };
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.UpdateMyProfile(request);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "New password must be different from the current password.";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Fact]
    public async Task UpdateMyProfile_WhenPasswordUpdateRequestedWithMisMatchingNewAndConfirmNewPassword_ReturnsBadRequestResultWithValidationProblemDetails()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var request = new UserUpdateRequest
        {
            CurrentPassword = "test_password",
            NewPassword = "new_test_password",
            ConfirmNewPassword = "confirm_new_test_password"
        };
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.UpdateMyProfile(request);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "New passwords do not match.";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }

    [Fact]
    public async Task UpdateMyProfile_WhenRequestCurrentPasswordNotEqualCurrentPassword_ReturnsBadRequestResultWithValidationProblemDetails()
    {
        // Arrange
        var (db, connection) = TestDbContextFactory.CreateSqliteDbContext();

        try
        {
            var testUser = await DataSeedHelper.SeedUserDataAsync(db);
            var request = new UserUpdateRequest
            {
                CurrentPassword = "test_password",
                NewPassword = "new_test_password",
                ConfirmNewPassword = "new_test_password"
            };

            _userManagerMock
                .Setup(x => x.ChangePasswordAsync(It.IsAny<ApplicationUser>(),
                    It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "PasswordMismatch",
                        Description = "Current passwords do not match."
                    }));

            HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);

            // Act
            var result = await _controller.UpdateMyProfile(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);

            var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
            const string error = "Current passwords do not match.";
            Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
        }
        finally
        {
            // Always dispose even if assertions fail
            await db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }


    [Fact]
    public async Task UpdateMyProfile_WithValidUserNameUpdateRequest_ReturnsNoContentResult()
    {
        // Arrange
        var (db, connection) = TestDbContextFactory.CreateSqliteDbContext();

        try
        {
            var testUser = await DataSeedHelper.SeedUserDataAsync(db);
            var request = new UserUpdateRequest
            {
                NewUserName = "new_user_name"
            };

            _userManagerMock
                .Setup(x => x.SetUserNameAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);

            // Act
            var result = await _controller.UpdateMyProfile(request);

            // Assert
            var noContentResult = Assert.IsType<NoContentResult>(result);
            Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
        }
        finally
        {
            await db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task UpdateMyProfile_WithValidPasswordUpdateRequest_ReturnsNoContentResult()
    {
        // Arrange
        var (db, connection) = TestDbContextFactory.CreateSqliteDbContext();
        try
        {
            var testUser = await DataSeedHelper.SeedUserDataAsync(db);
            var request = new UserUpdateRequest
            {
                CurrentPassword = "password",
                NewPassword = "new_password",
                ConfirmNewPassword = "new_password"
            };

            _userManagerMock
                .Setup(x => x.ChangePasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);

            // Act
            var result = await _controller.UpdateMyProfile(request);

            // Assert
            var noContentResult = Assert.IsType<NoContentResult>(result);
            Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
        }
        finally
        {
            await db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetUserProfile_ForUnauthorizedUser_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        const string testId = "test_user_id";
        
        // Act
        var result = await _controller.GetUserProfile(testId);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetUserProfile_ForNullOrEmptyUserId_ReturnsBadRequestResultWithValidationProblemDetails(string? testUserToSearchId)
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.GetUserProfile(testUserToSearchId!);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "User id is required.";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Fact]
    public async Task GetUserProfile_ForNonExistentUser_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        const string testUserToSearchId = "test_user_to_search_id";
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.GetUserProfile(testUserToSearchId);
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Not found", problemDetails.Title);
    }

    [Fact]
    public async Task GetUserProfile_WithValidInputData_ReturnsOkResultWithProfileResponse()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var testUserToSearch = UserHelper.CreateUser();
        testUserToSearch.Id = "test_user_to_search_id";
        _userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(testUserToSearch);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.GetUserProfile(testUserToSearch.Id);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var profileResponse = Assert.IsType<ProfileResponse>(okResult.Value);
        Assert.Equal(testUserToSearch.Id, profileResponse.Id);
    }
}