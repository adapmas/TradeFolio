using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TradeService.Infrastructure.Persistence;

namespace TradeService.Application;

public interface IUserRegistrar
{
    /// <summary>Returns the new user, or null if the email is already registered (ignoring case).</summary>
    Task<User?> RegisterAsync(string email, string password, CancellationToken cancellationToken);
}

public sealed class UserRegistrar(TradeDbContext db, IPasswordHasher<User> hasher) : IUserRegistrar
{
    private const string EmailIndexName = "ux_users_email";

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public async Task<User?> RegisterAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(email);

        // Fast path for the ordinary duplicate.
        if (await db.Users.AnyAsync(u => u.Email == normalized, cancellationToken))
        {
            return null;
        }

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = normalized,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = UserStatus.Active
        };

        // Only the salted hash is ever stored; the plain password is never kept or logged.
        user.PasswordHash = hasher.HashPassword(user, password);

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsEmailViolation(ex))
        {
            // Two requests registered the same email at once; the unique index stopped this one.
            db.Entry(user).State = EntityState.Detached;
            return null;
        }

        return user;
    }

    private static bool IsEmailViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: EmailIndexName
        };
}