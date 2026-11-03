using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TradeFolio.Common.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    private const int MinKeyBytes = 32;   // HS256 needs a 256-bit key

    public string Issuer { get; set; } = "tradefolio";
    public string Audience { get; set; } = "tradefolio-api";

    /// <summary>Comes only from the environment (Jwt__SigningKey), never from source control.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(AccessTokenMinutes);

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));

    /// <summary>One definition of "a valid token", used by the API and by the tests.</summary>
    public TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = CreateSigningKey(),
        RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

        ValidateIssuer = true,
        ValidIssuer = Issuer,

        ValidateAudience = true,
        ValidAudience = Audience,

        ValidateLifetime = true,
        RequireExpirationTime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    /// <summary>Fails fast at startup instead of issuing weak or unusable tokens.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            throw new InvalidOperationException(
                "JWT signing key is missing. Set the Jwt__SigningKey environment variable (at least 32 bytes).");
        }

        if (Encoding.UTF8.GetByteCount(SigningKey) < MinKeyBytes)
        {
            throw new InvalidOperationException(
                $"JWT signing key is too short. It must be at least {MinKeyBytes} bytes.");
        }

        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("JWT issuer and audience must not be empty.");
        }

        if (AccessTokenMinutes is < 1 or > 60)
        {
            throw new InvalidOperationException("JWT access token lifetime must be between 1 and 60 minutes.");
        }
    }
}
