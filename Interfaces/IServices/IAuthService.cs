using Microsoft.AspNetCore.Identity;

namespace SystemManagementSystem.Services;

public interface IAuthService
{
    Task<IdentityResult> RegisterUserAsync(string email, string password, string role);
    Task<IdentityResult> VerifyEmailAsync(string email, string code);
    Task<IdentityResult> ResendVerificationCode(string email);
}