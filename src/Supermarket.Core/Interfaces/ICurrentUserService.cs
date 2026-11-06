namespace Supermarket.Core.Interfaces;

/// <summary>
/// Abstraction used by the DbContext to stamp the audit columns without knowing
/// anything about the WPF applications that consume the library.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>User name of the operator currently signed in, or null when the system performs the action.</summary>
    string? UserName { get; }

    /// <summary>
    /// Identifier of the signed-in operator, or null for system operations. Set by the shells after
    /// sign-in so the administration service can protect the current account from locking itself out.
    /// </summary>
    int? UserId => null;
}

/// <summary>
/// Default implementation used by background / seed operations.
/// </summary>
public sealed class SystemUserService : ICurrentUserService
{
    public string? UserName => "system";

    public int? UserId => null;
}

/// <summary>
/// Testable clock. Keeps <c>DateTime.UtcNow</c> out of the business services.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }

    DateTime Now { get; }

    DateOnly Today { get; }
}

/// <summary>Default clock implementation.</summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime Now => DateTime.Now;

    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}
