using Supermarket.Core.Common;

namespace Supermarket.Core.DTOs;

/// <summary>One user row on the administration screen.</summary>
public sealed class UserListItemDto
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public string RoleName => Role.ToArabic();

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }

    public string StatusName => IsActive ? "نشط" : "موقوف";

    public DateTime? LastLoginAtUtc { get; set; }

    /// <summary>True when the user has an override row that differs from the role default.</summary>
    public bool HasCustomPermissions { get; set; }

    /// <summary>Number of granted permissions after applying the overrides.</summary>
    public int GrantedPermissionCount { get; set; }
}

/// <summary>
/// Create / update payload of a user. <see cref="Password"/> and <see cref="Pin"/> are optional on
/// update: an empty value keeps the current secret.
/// </summary>
public sealed class UserEditDto
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Cashier;

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Minimum 6 characters; ignored when empty and <see cref="Id"/> &gt; 0.</summary>
    public string? Password { get; set; }

    /// <summary>Exactly 4 digits; ignored when empty and <see cref="Id"/> &gt; 0.</summary>
    public string? Pin { get; set; }
}

/// <summary>One row of the permissions matrix: a key, its Arabic caption, the default and the override.</summary>
public sealed class PermissionRowDto
{
    public string Key { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;

    /// <summary>Effective value (what the application actually allows).</summary>
    public bool IsGranted { get; set; }

    /// <summary>Value granted by the role when no override exists.</summary>
    public bool RoleDefault { get; set; }

    /// <summary>True when the current value differs from the role default (an override row exists).</summary>
    public bool IsOverride => IsGranted != RoleDefault;
}

/// <summary>The whole permissions screen of one user.</summary>
public sealed class UserPermissionsDto
{
    public int UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public string RoleName => Role.ToArabic();

    public List<PermissionRowDto> Rows { get; set; } = new();
}

/// <summary>Result of an administrative operation (create / update / reset).</summary>
public sealed class OperationResultDto
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public int? Id { get; init; }

    public static OperationResultDto Ok(int? id = null) => new() { Succeeded = true, Id = id };

    public static OperationResultDto Fail(string message) => new() { Succeeded = false, ErrorMessage = message };
}
