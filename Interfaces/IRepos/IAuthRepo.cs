using Microsoft.AspNetCore.Identity;
using SystemManagementSystem.Models;

namespace SystemManagementSystem.Interfaces.IRepos;

public interface IAuthRepo
{
    Task<(IdentityResult Result, VerificationCode? VerificationCode)> RegisterUserAsync(
        string email,
        string password,
        string role);

    Task<(IdentityResult Result, VerificationCode? VerificationCode)> ResendVerificationCodeAsync(
        string email);

    Task<IdentityResult> VerifyEmailAsync(string email, string code);
}
