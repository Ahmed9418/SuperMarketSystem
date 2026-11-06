using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Supermarket.Core.Common;
using Supermarket.Core.Data;
using Supermarket.Core.DTOs;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>
/// User and permission administration (Phase 5).
///
/// <para>
/// The last active administrator can never be deactivated or demoted, and a user cannot lock
/// themselves out by removing their own administration rights: both are checked here, not only in the
/// UI, because the same service is reachable from the future API surface.
/// </para>
/// </summary>
public sealed class UserAdminService : IUserAdminService
{
    private readonly SupermarketDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<UserAdminService> _logger;

    public UserAdminService(
        SupermarketDbContext db,
        IPasswordHasher<User> hasher,
        ICurrentUserService currentUser,
        IDateTimeProvider clock,
        ILogger<UserAdminService> logger)
    {
        _db = db;
        _hasher = hasher;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserListItemDto>> GetAllAsync(bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        var query = _db.Users.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(u => u.IsActive);
        }

        var users = await query
            .OrderBy(u => u.Role)
            .ThenBy(u => u.FullName)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.FullName,
                u.Role,
                u.PhoneNumber,
                u.IsActive,
                u.LastLoginAtUtc,
                Overrides = u.Permissions.Select(p => new { p.PermissionKey, p.IsGranted }).ToList()
            })
            .ToListAsync(cancellationToken);

        var result = new List<UserListItemDto>(users.Count);

        foreach (var user in users)
        {
            var effective = ResolveEffective(user.Role, user.Overrides.Select(o => (o.PermissionKey, o.IsGranted)));

            result.Add(new UserListItemDto
            {
                Id = user.Id,
                UserName = user.UserName,
                FullName = user.FullName,
                Role = user.Role,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                LastLoginAtUtc = user.LastLoginAtUtc,
                HasCustomPermissions = user.Overrides.Count > 0,
                GrantedPermissionCount = effective.Count
            });
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<UserEditDto?> GetForEditAsync(int userId, CancellationToken cancellationToken = default) =>
        await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserEditDto
            {
                Id = u.Id,
                UserName = u.UserName,
                FullName = u.FullName,
                Role = u.Role,
                PhoneNumber = u.PhoneNumber,
                IsActive = u.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<OperationResultDto> CreateAsync(UserEditDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var validation = Validate(dto, isNew: true);
        if (validation is not null)
        {
            return OperationResultDto.Fail(validation);
        }

        var userName = dto.UserName.Trim();
        if (await _db.Users.AnyAsync(u => u.UserName == userName, cancellationToken))
        {
            return OperationResultDto.Fail("اسم المستخدم مستخدم بالفعل");
        }

        var user = new User
        {
            UserName = userName,
            FullName = dto.FullName.Trim(),
            Role = dto.Role,
            PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim(),
            IsActive = dto.IsActive
        };

        user.PasswordHash = _hasher.HashPassword(user, dto.Password!);
        user.PinHash = _hasher.HashPassword(user, dto.Pin!);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserName} created with role {Role}", user.UserName, user.Role);

        return OperationResultDto.Ok(user.Id);
    }

    /// <inheritdoc />
    public async Task<OperationResultDto> UpdateAsync(UserEditDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var validation = Validate(dto, isNew: false);
        if (validation is not null)
        {
            return OperationResultDto.Fail(validation);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == dto.Id, cancellationToken);
        if (user is null)
        {
            return OperationResultDto.Fail("المستخدم غير موجود");
        }

        var userName = dto.UserName.Trim();
        if (await _db.Users.AnyAsync(u => u.UserName == userName && u.Id != dto.Id, cancellationToken))
        {
            return OperationResultDto.Fail("اسم المستخدم مستخدم بالفعل");
        }

        // Guard rail: never leave the system without an active administrator.
        var losesAdministration = user.Role == UserRole.Administrator
                                  && (dto.Role != UserRole.Administrator || !dto.IsActive);

        if (losesAdministration && await CountActiveAdministratorsAsync(cancellationToken) <= 1)
        {
            return OperationResultDto.Fail("لا يمكن إيقاف أو تغيير دور آخر مدير نظام في النظام");
        }

        if (dto.Id == _currentUser.UserId && (!dto.IsActive || dto.Role != user.Role && dto.Role != UserRole.Administrator))
        {
            return OperationResultDto.Fail("لا يمكنك تغيير دورك أو إيقاف حسابك أثناء استخدام النظام");
        }

        user.UserName = userName;
        user.FullName = dto.FullName.Trim();
        user.Role = dto.Role;
        user.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();
        user.IsActive = dto.IsActive;

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            user.PasswordHash = _hasher.HashPassword(user, dto.Password);
        }

        if (!string.IsNullOrWhiteSpace(dto.Pin))
        {
            user.PinHash = _hasher.HashPassword(user, dto.Pin);
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} updated (role {Role}, active {IsActive})", user.Id, user.Role, user.IsActive);

        return OperationResultDto.Ok(user.Id);
    }

    /// <inheritdoc />
    public async Task<OperationResultDto> SetActiveAsync(int userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return OperationResultDto.Fail("المستخدم غير موجود");
        }

        if (!isActive && user.Id == _currentUser.UserId)
        {
            return OperationResultDto.Fail("لا يمكنك إيقاف حسابك الحالي");
        }

        if (!isActive && user.Role == UserRole.Administrator && await CountActiveAdministratorsAsync(cancellationToken) <= 1)
        {
            return OperationResultDto.Fail("لا يمكن إيقاف آخر مدير نظام في النظام");
        }

        user.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);

        return OperationResultDto.Ok(user.Id);
    }

    /// <inheritdoc />
    public async Task<OperationResultDto> ResetPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            return OperationResultDto.Fail("كلمة المرور يجب أن تكون 6 أحرف على الأقل");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return OperationResultDto.Fail("المستخدم غير موجود");
        }

        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset for user {UserId} by {Operator}", userId, _currentUser.UserName);

        return OperationResultDto.Ok(user.Id);
    }

    /// <inheritdoc />
    public async Task<OperationResultDto> ResetPinAsync(int userId, string newPin, CancellationToken cancellationToken = default)
    {
        if (!IsValidPin(newPin))
        {
            return OperationResultDto.Fail("الرقم السري يجب أن يكون 4 أرقام");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return OperationResultDto.Fail("المستخدم غير موجود");
        }

        user.PinHash = _hasher.HashPassword(user, newPin);
        await _db.SaveChangesAsync(cancellationToken);

        return OperationResultDto.Ok(user.Id);
    }

    /// <inheritdoc />
    public async Task<UserPermissionsDto?> GetPermissionsAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Role,
                Overrides = u.Permissions.Select(p => new { p.PermissionKey, p.IsGranted }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var overrides = user.Overrides
            .GroupBy(o => o.PermissionKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().IsGranted, StringComparer.OrdinalIgnoreCase);

        var defaults = AppPermissions.DefaultsFor(user.Role);

        return new UserPermissionsDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            Role = user.Role,
            Rows = AppPermissions.Catalog
                .Select(entry => new PermissionRowDto
                {
                    Key = entry.Key,
                    ArabicName = entry.ArabicName,
                    Group = entry.Group,
                    RoleDefault = defaults.Contains(entry.Key),
                    IsGranted = overrides.TryGetValue(entry.Key, out var granted) ? granted : defaults.Contains(entry.Key)
                })
                .ToList()
        };
    }

    /// <inheritdoc />
    public async Task<OperationResultDto> SavePermissionsAsync(UserPermissionsDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var user = await _db.Users
            .Include(u => u.Permissions)
            .FirstOrDefaultAsync(u => u.Id == dto.UserId, cancellationToken);

        if (user is null)
        {
            return OperationResultDto.Fail("المستخدم غير موجود");
        }

        var defaults = AppPermissions.DefaultsFor(user.Role);

        if (dto.UserId == _currentUser.UserId)
        {
            var hadUsersManage = defaults.Contains(AppPermissions.UsersManage)
                ? user.Permissions.All(p => p.PermissionKey != AppPermissions.UsersManage || p.IsGranted)
                : user.Permissions.Any(p => p.PermissionKey == AppPermissions.UsersManage && p.IsGranted);

            if (hadUsersManage && dto.Rows.Any(r => r.Key == AppPermissions.UsersManage && !r.IsGranted))
            {
                return OperationResultDto.Fail("لا يمكنك إزالة صلاحية إدارة المستخدمين من حسابك الحالي");
            }
        }

        foreach (var row in dto.Rows)
        {
            var existing = user.Permissions.FirstOrDefault(p =>
                string.Equals(p.PermissionKey, row.Key, StringComparison.OrdinalIgnoreCase));

            var isDefault = defaults.Contains(row.Key);

            if (row.IsGranted == isDefault)
            {
                // Back to the role default: the override row is simply removed.
                if (existing is not null)
                {
                    _db.UserPermissions.Remove(existing);
                }

                continue;
            }

            if (existing is null)
            {
                user.Permissions.Add(new UserPermission
                {
                    UserId = user.Id,
                    PermissionKey = row.Key,
                    IsGranted = row.IsGranted
                });
            }
            else
            {
                existing.IsGranted = row.IsGranted;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permissions of user {UserId} saved by {Operator}", dto.UserId, _currentUser.UserName);

        return OperationResultDto.Ok(user.Id);
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Role,
                Overrides = u.Permissions.Select(p => new { p.PermissionKey, p.IsGranted }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return user is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : ResolveEffective(user.Role, user.Overrides.Select(o => (o.PermissionKey, o.IsGranted)));
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    private static HashSet<string> ResolveEffective(
        UserRole role,
        IEnumerable<(string PermissionKey, bool IsGranted)> overrides)
    {
        var effective = AppPermissions.DefaultsFor(role).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in overrides)
        {
            if (row.IsGranted)
            {
                effective.Add(row.PermissionKey);
            }
            else
            {
                effective.Remove(row.PermissionKey);
            }
        }

        return effective;
    }

    private Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken) =>
        _db.Users.CountAsync(u => u.Role == UserRole.Administrator && u.IsActive, cancellationToken);

    private static string? Validate(UserEditDto dto, bool isNew)
    {
        if (string.IsNullOrWhiteSpace(dto.UserName) || dto.UserName.Trim().Length < 3)
        {
            return "اسم المستخدم يجب أن يكون 3 أحرف على الأقل";
        }

        if (string.IsNullOrWhiteSpace(dto.FullName) || dto.FullName.Trim().Length < 3)
        {
            return "الاسم الكامل مطلوب";
        }

        if (isNew)
        {
            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            {
                return "كلمة المرور يجب أن تكون 6 أحرف على الأقل";
            }

            if (!IsValidPin(dto.Pin))
            {
                return "الرقم السري يجب أن يكون 4 أرقام";
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(dto.Password) && dto.Password.Length < 6)
            {
                return "كلمة المرور يجب أن تكون 6 أحرف على الأقل";
            }

            if (!string.IsNullOrWhiteSpace(dto.Pin) && !IsValidPin(dto.Pin))
            {
                return "الرقم السري يجب أن يكون 4 أرقام";
            }
        }

        if (!Enum.IsDefined(dto.Role))
        {
            return "الدور غير صحيح";
        }

        return null;
    }

    private static bool IsValidPin(string? pin) =>
        !string.IsNullOrWhiteSpace(pin) && pin.Length == 4 && pin.All(char.IsDigit);
}
