using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DormGO.Tests.Helpers;

public static class UserManagerMockHelper
{
    public static Mock<UserManager<TUser>> GetUserManagerMock<TUser>() where TUser : class
    {
        var store = new Mock<IUserStore<TUser>>();
        var options = new Mock<IOptions<IdentityOptions>>();
        var idOptions = new IdentityOptions();
        options.Setup(o => o.Value).Returns(idOptions);
        var userValidators = new List<IUserValidator<TUser>>();
        var validator = new Mock<IUserValidator<TUser>>();
        userValidators.Add(validator.Object);
        var pwdValidators = new List<IPasswordValidator<TUser>>();
        var pwdValidator = new PasswordValidator<TUser>();
        pwdValidators.Add(pwdValidator);
        
        var userManager = new Mock<UserManager<TUser>>(
            store.Object,
            options.Object,
            new PasswordHasher<TUser>(),
            userValidators,
            pwdValidators,
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null,
            new Mock<ILogger<UserManager<TUser>>>().Object);
        
        userManager.Setup(x => x.GetUserIdAsync(It.IsAny<TUser>()))
            .ReturnsAsync((TUser user) => "test-user-id");
        userManager.Setup(x => x.GetUserNameAsync(It.IsAny<TUser>()))
            .ReturnsAsync((TUser user) => "test-username");
        userManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<TUser>()))
            .ReturnsAsync("test-confirmation-token");

        return userManager;
    }
}