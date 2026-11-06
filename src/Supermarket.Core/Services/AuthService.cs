using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Supermarket.Core.Data;
using Supermarket.Core.DTOs;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>
/// Authentication shared by the Admin application (full password) and the POS (4 digit PIN).
/// Passwords are verified with <see cref="PasswordHasher{TUser}"/> (PBKDF2, 100k iterations).
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly SupermarketDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        SupermarketDbContext db,
        IPasswordHasher<User> hasher,
        IDateTimeProvider clock,
        ILogger<AuthService> logger)
    {
        _db = db;
        _hasher = hasher;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AuthResult> SignInAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return AuthResult.Failure("من فضلك أدخل اسم المستخدم وكلمة المرور");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == userName.Trim(), cancellationToken);
        if (user is null)
        {
            return AuthResult.Failure("اسم المستخدم أو كلمة المرور غير صحيحة");
        }

        if (!user.IsActive)
        {
            return AuthResult.Failure("هذا الحساب موقوف، برجاء مراجعة مدير النظام");
        }

        var verification = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Failed password sign-in for {UserName}", userName);
            return AuthResult.Failure("اسم المستخدم أو كلمة المرور غير صحيحة");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, password);
        }

        user.LastLoginAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(ToDto(user));
    }

    public async Task<AuthResult> SignInWithPinAsync(string userName, string pin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(pin))
        {
            return AuthResult.Failure("من فضلك أدخل اسم المستخدم والرقم السري");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == userName.Trim(), cancellationToken);
        if (user is null || string.IsNullOrEmpty(user.PinHash))
        {
            return AuthResult.Failure("اسم المستخدم أو الرقم السري غير صحيح");
        }

        if (!user.IsActive)
        {
            return AuthResult.Failure("هذا الحساب موقوف، برجاء مراجعة مدير النظام");
        }

        var verification = _hasher.VerifyHashedPassword(user, user.PinHash, pin);
        if (verification == PasswordVerificationResult.Failed)
        {
            return AuthResult.Failure("اسم المستخدم أو الرقم السري غير صحيح");
        }

        user.LastLoginAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(ToDto(user));
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            throw new ArgumentException("كلمة المرور الجديدة يجب أن تكون 6 أحرف على الأقل");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        var verification = _hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        if (verification == PasswordVerificationResult.Failed)
        {
            return false;
        }

        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static UserDto ToDto(User user) => new()
    {
        Id = user.Id,
        UserName = user.UserName,
        FullName = user.FullName,
        Role = user.Role,
        PhoneNumber = user.PhoneNumber,
        IsActive = user.IsActive,
        LastLoginAtUtc = user.LastLoginAtUtc
    };
}
