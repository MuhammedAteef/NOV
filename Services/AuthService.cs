using Microsoft.AspNetCore.Identity;
using SystemManagementSystem.Interfaces.IRepos;


namespace SystemManagementSystem.Services;

public class AuthService : IAuthService
{
    private readonly IAuthRepo _authRepo;
    private readonly IEmailService _emailService;

    public AuthService(IAuthRepo authRepo, IEmailService emailService)
    {
        _authRepo = authRepo;
        _emailService = emailService;
    }

    public async Task<IdentityResult> RegisterUserAsync(string email, string password, string role)
    {
        var (result, verificationCode) = await _authRepo.RegisterUserAsync(email, password, role);
        if (result.Succeeded && verificationCode is not null)
        {
            await _emailService.SendEmailAsync(email,
                             "NOVERP - Email Verification Code",
                              $"<h3>Welcome to NOVERP,</h3><p>Your email verification code is: <strong>{verificationCode.Code}</strong></p><p>This code will expire in 15 minutes.</p>");

        }

        return result;
    }

    public async Task<IdentityResult> ResendVerificationCode(string email)
    {
        var (result, verificationCode) = await _authRepo.ResendVerificationCodeAsync(email);
        if (result.Succeeded && verificationCode is not null)
        {
            await _emailService.SendEmailAsync(email,
                         "NOVERP - Email Verification Code",
                          $"<h3>Welcome to NOVERP,</h3><p>Your email verification code is: <strong>{verificationCode.Code}</strong></p><p>This code will expire in 15 minutes.</p>");
        }

        return result;
    }

    public async Task<IdentityResult> VerifyEmailAsync(string email, string code)
    {
        return await _authRepo.VerifyEmailAsync(email, code);
    }
}