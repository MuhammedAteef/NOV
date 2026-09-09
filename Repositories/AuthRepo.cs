using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.Interfaces.IRepos;
using SystemManagementSystem.Models;

namespace SystemManagementSystem.Repositories;

public class AuthRepo : IAuthRepo
{
    private const string EmailVerificationType = "EmailVerification";

    private readonly SalesManagementSystemContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public AuthRepo(
        SalesManagementSystemContext context,
        UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<(IdentityResult Result, VerificationCode? VerificationCode)> RegisterUserAsync(
        string email,
        string password,
        string role)
    {
        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return (result, null);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            return (roleResult, null);
        }

        var verificationCode = CreateVerificationCode(user.Id);
        _context.VerificationCodes.Add(verificationCode);
        await _context.SaveChangesAsync();

        return (IdentityResult.Success, verificationCode);
    }

    public async Task<(IdentityResult Result, VerificationCode? VerificationCode)> ResendVerificationCodeAsync(
        string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Failed("Email is required.");
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Failed("User not found.");
        }

        if (user.EmailConfirmed)
        {
            return Failed("Email is already verified.");
        }

        var verificationCode = CreateVerificationCode(user.Id);
        _context.VerificationCodes.Add(verificationCode);
        await _context.SaveChangesAsync();

        return (IdentityResult.Success, verificationCode);
    }

    public async Task<IdentityResult> VerifyEmailAsync(string email, string code)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError { Description = "User not found." });
        }

        var verificationCode = await _context.VerificationCodes
            .Where(item => item.UserId == user.Id &&
                          item.Type == EmailVerificationType &&
                          item.UsedAt == null)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync();

        if (verificationCode is null ||
            verificationCode.Code != code ||
            verificationCode.ExpirationDate <= DateTime.UtcNow)
        {
            return IdentityResult.Failed(new IdentityError { Description = "Invalid verification code." });
        }

        user.EmailConfirmed = true;
        verificationCode.UsedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return result;
        }

        await _context.SaveChangesAsync();
        return IdentityResult.Success;
    }

    private static VerificationCode CreateVerificationCode(string userId)
    {
        return new VerificationCode
        {
            UserId = userId,
            Code = RandomNumberGenerator.GetInt32(100000, 999999).ToString(),
            ExpirationDate = DateTime.UtcNow.AddMinutes(15),
            Type = EmailVerificationType,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static (IdentityResult Result, VerificationCode? VerificationCode) Failed(string description)
    {
        return (IdentityResult.Failed(new IdentityError { Description = description }), null);
    }
}
