using System.Security.Cryptography;
using System.Text;
using Brokerage.Application.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Infrastructure.Persistence;

public sealed class PersistentOtpService(BrokerageDbContext db, IOtpCodeHasher codeHasher) : IOtpService
{
    private const int MaxAttempts = 5;

    public async Task<OtpChallenge> IssueAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow;

        // SQLite cannot translate comparisons over DateTimeOffset consistently.
        // Materialize the small challenge set, then identify expired records in memory.
        var existingChallenges = await db.OtpChallenges.ToListAsync(cancellationToken);
        var expiredChallenges = existingChallenges.Where(x => x.ExpiresAt <= now).ToArray();
        db.OtpChallenges.RemoveRange(expiredChallenges);

        var challengeId = Guid.NewGuid().ToString("N");
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var expiresAt = now.AddMinutes(2);

        db.OtpChallenges.Add(new OtpChallengeRecord
        {
            ChallengeId = challengeId,
            UserId = userId,
            CodeHash = codeHasher.Hash(code),
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

        var suppliedHash = codeHasher.Hash(code ?? string.Empty);
        var valid = CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(challenge.CodeHash),
            Convert.FromHexString(suppliedHash));

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
}

public interface IOtpCodeHasher
{
    string Hash(string value);
}

public sealed class HmacOtpCodeHasher : IOtpCodeHasher
{
    private readonly byte[] _key;

    public HmacOtpCodeHasher(byte[] key)
    {
        if (key is null || key.Length < 32)
            throw new ArgumentException("OTP HMAC key must be at least 256 bits.", nameof(key));

        _key = key.ToArray();
    }

    public string Hash(string value)
    {
        using var hmac = new HMACSHA256(_key);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }
}