using Supermarket.Core.DTOs;

namespace Supermarket.Core.Interfaces;

/// <summary>User, role and permission administration (Phase 5).</summary>
public interface IUserAdminService
{
    /// <summary>Every account with its role and effective permission count.</summary>
    Task<IReadOnlyList<UserListItemDto>> GetAllAsync(bool includeInactive = true, CancellationToken cancellationToken = default);

    /// <summary>The editable projection of one account (never includes the hashes).</summary>
    Task<UserEditDto?> GetForEditAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Creates an account; the password and the 4 digit PIN are both required.</summary>
    Task<OperationResultDto> CreateAsync(UserEditDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates an account; empty password / PIN keep the current secrets.</summary>
    Task<OperationResultDto> UpdateAsync(UserEditDto dto, CancellationToken cancellationToken = default);

    /// <summary>Activates or suspends an account (the last administrator is protected).</summary>
    Task<OperationResultDto> SetActiveAsync(int userId, bool isActive, CancellationToken cancellationToken = default);

    Task<OperationResultDto> ResetPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default);

    Task<OperationResultDto> ResetPinAsync(int userId, string newPin, CancellationToken cancellationToken = default);

    /// <summary>The permission matrix of one user, defaults included.</summary>
    Task<UserPermissionsDto?> GetPermissionsAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Persists the matrix: only the rows that differ from the role default are stored.</summary>
    Task<OperationResultDto> SavePermissionsAsync(UserPermissionsDto dto, CancellationToken cancellationToken = default);

    /// <summary>Effective permission keys of a user (role defaults + overrides).</summary>
    Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(int userId, CancellationToken cancellationToken = default);
}
