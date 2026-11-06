using Supermarket.Core.Common;
using Supermarket.Core.DependencyInjection;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.Services;

/// <summary>
/// Holds the signed-in operator for the lifetime of the process and keeps the shared
/// <see cref="MutableCurrentUserService"/> (used for the audit columns) in sync.
///
/// <para>
/// The effective permission set is loaded from <see cref="IUserAdminService"/> at sign-in, so the
/// menu and every screen check a permission key instead of hard-coding a role comparison. A menu item
/// is visible when its key is granted.
/// </para>
/// </summary>
public sealed class AuthSession
{
    private readonly MutableCurrentUserService _currentUser;
    private HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase);

    public AuthSession(MutableCurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public UserDto? CurrentUser { get; private set; }

    public bool IsAuthenticated => CurrentUser is not null;

    public bool IsAdministrator => CurrentUser?.Role == UserRole.Administrator;

    public string DisplayName => CurrentUser?.FullName ?? string.Empty;

    public string RoleNameArabic => CurrentUser?.RoleArabic ?? string.Empty;

    /// <summary>Effective permission keys of the signed-in operator.</summary>
    public IReadOnlyCollection<string> Permissions => _permissions;

    /// <summary>True when the operator holds the permission (administrators hold everything).</summary>
    public bool Can(string permissionKey) =>
        CurrentUser?.Role == UserRole.Administrator || _permissions.Contains(permissionKey);

    /// <summary>Stores the authenticated user and publishes it to the audit layer.</summary>
    public void SignIn(UserDto user)
    {
        CurrentUser = user;
        _currentUser.UserName = user.UserName;
        _currentUser.UserId = user.Id;
        _currentUser.Role = user.Role;

        // Role defaults are known without a round trip; the overrides replace them right after.
        _permissions = AppPermissions.DefaultsFor(user.Role).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Replaces the permission set with the effective one read from the database. Called right after
    /// <see cref="SignIn"/>; a failure keeps the role defaults instead of locking the operator out.
    /// </summary>
    public async Task LoadPermissionsAsync(IUserAdminService userAdminService, CancellationToken cancellationToken = default)
    {
        if (CurrentUser is null)
        {
            return;
        }

        try
        {
            var effective = await userAdminService.GetEffectivePermissionsAsync(CurrentUser.Id, cancellationToken);
            if (effective.Count > 0 || CurrentUser.Role != UserRole.Administrator)
            {
                _permissions = effective.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // Keep the role defaults: never block the back office because of a permission read.
        }
    }

    /// <summary>Clears the session on sign-out.</summary>
    public void SignOut()
    {
        CurrentUser = null;
        _permissions.Clear();
        _currentUser.Clear();
    }
}
