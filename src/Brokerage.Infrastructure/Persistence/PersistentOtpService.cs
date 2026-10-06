using System.Security.Cryptography;
using System.Text;
using Brokerage.Application.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Infrastructure.Persistence;

public sealed class PersistentOtpService(BrokerageDbContext db) : IOtpService
{
    private const int MaxAttempts = 5;

    public async Task<OtpChallenge> IssueAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var challengeId = Guid.NewGuid().ToString("N");
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(2);

        db.OtpChallenges.Add(new OtpChallengeRecord
        {
            ChallengeId = challengeId,
            UserId = userId,
            CodeHash = Hash(code),
            ExpiresAt = expiresAt,
            FailedAttempts = 0
        });
        await db.SaveChangesAsync(cancellationToken);
        return new OtpChallenge(challengeId, expiresAt);
    }

    public async Task<bool> VerifyAsync(string userId, string challengeId, string code, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var challenge = await db.OtpChallenges.SingleOrDefaultAsync(
            x => x.ChallengeId == challengeId && x.UserId == userId, cancellationToken);

        if (challenge is null)
            return false;

        if (challenge.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            db.OtpChallenges.Remove(challenge);
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        var suppliedHash = Hash(code ?? string.Empty);
        var valid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(challenge.CodeHash),
            Encoding.UTF8.GetBytes(suppliedHash));

        if (valid)
        {
            db.OtpChallenges.Remove(challenge);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        challenge.FailedAttempts++;
        if (challenge.FailedAttempts >= MaxAttempts)
            db.OtpChallenges.Remove(challenge);

        await db.SaveChangesAsync(cancellationToken);
        return false;
    }

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
