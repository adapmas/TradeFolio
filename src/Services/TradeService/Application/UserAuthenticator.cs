using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TradeService.Infrastructure.Persistence;

namespace TradeService.Application;

public interface IUserAuthenticator
{
    /// <summary>Returns the user, or null for ANY failure (unknown email, wrong password, disabled).</summary>
    Task<User?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken);
}

public sealed class UserAuthenticator(TradeDbContext db, IPasswordHasher<User> hasher) : IUserAuthenticator
{
    // Unknown emails still pay for one password verification, so response time does not
    // reveal whether an email is registered.
    private static readonly string DummyHash =
        new PasswordHasher<User>().HashPassword(new User(), Guid.NewGuid().ToString("N"));

    public async Task<User?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalized = UserRegistrar.NormalizeEmail(email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);

        if (user is null)
        {
            hasher.VerifyHashedPassword(new User(), DummyHash, password);
            return null;
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);

        if (result == PasswordVerificationResult.Failed || user.Status != UserStatus.Active)
        {
            return null;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, password);
            await db.SaveChangesAsync(cancellationToken);
        }

        return user;
    }
}