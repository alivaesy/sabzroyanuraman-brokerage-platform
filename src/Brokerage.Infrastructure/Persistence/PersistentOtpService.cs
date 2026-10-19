using System.Security.Cryptography;
using System.Text;
using Brokerage.Application.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Infrastructure.Persistence;

public sealed class PersistentOtpService(BrokerageDbContext db, IOtpCodeHasher codeHasher) : IOtpService
{
    private const int MaxAttempts = 5;
    private const int MaxUserIdLength = 200;

    public async Task<OtpChallenge> IssueAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID must not be blank.", nameof(userId));
        if (userId.Length > MaxUserIdLength)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID cannot exceed the persistence limit of 200 characters.");

        var now = DateTimeOffset.UtcNow;

        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM otp_challenges WHERE ExpiresAt <= {now}",
                cancellationToken);
        }
        else
        {
            await db.OtpChallenges
                .Where(x => x.ExpiresAt <= now)
                .ExecuteDeleteAsync(cancellationToken);
        }

        // Issuing a new challenge invalidates prior active challenges for this account.
        // This prevents an older code from remaining usable after a resend.
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM otp_challenges WHERE UserId = {userId} AND ExpiresAt > {now}",
                cancellationToken);
        }
        else
        {
            await db.OtpChallenges
                .Where(x => x.UserId == userId && x.ExpiresAt > now)
                .ExecuteDeleteAsync(cancellationToken);
        }

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
        if (string.IsNullOrWhiteSpace(userId)
            || string.IsNullOrWhiteSpace(challengeId)
            || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var challenge = await db.OtpChallenges.SingleOrDefaultAsync(
            x => x.ChallengeId == challengeId && x.UserId == userId, cancellationToken);

        if (challenge is null)
            return false;

        var now = DateTimeOffset.UtcNow;
        var isSqlite = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";

        if (challenge.ExpiresAt <= now)
        {
            if (isSqlite)
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM otp_challenges WHERE ChallengeId = {challengeId} AND UserId = {userId} AND ExpiresAt <= {now}",
                    cancellationToken);
            }
            else
            {
                await db.OtpChallenges
                    .Where(x => x.ChallengeId == challengeId && x.UserId == userId && x.ExpiresAt <= now)
                    .ExecuteDeleteAsync(cancellationToken);
            }

            return false;
        }

        byte[] storedHash;
        try
        {
            storedHash = Convert.FromHexString(challenge.CodeHash);
        }
        catch (FormatException)
        {
            // Corrupt persisted challenge data must fail closed, not turn OTP verification
            // into a server error or leave an unusable challenge active.
            if (isSqlite)
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM otp_challenges WHERE ChallengeId = {challengeId} AND UserId = {userId}",
                    cancellationToken);
            }
            else
            {
                await db.OtpChallenges
                    .Where(x => x.ChallengeId == challengeId && x.UserId == userId)
                    .ExecuteDeleteAsync(cancellationToken);
            }

            return false;
        }

        var suppliedHash = codeHasher.Hash(code);
        var valid = CryptographicOperations.FixedTimeEquals(
            storedHash,
            Convert.FromHexString(suppliedHash));

        if (valid)
        {
            var consumed = isSqlite
                ? await db.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM otp_challenges WHERE ChallengeId = {challengeId} AND UserId = {userId} AND ExpiresAt > {now} AND FailedAttempts < {MaxAttempts}",
                    cancellationToken)
                : await db.OtpChallenges
                    .Where(x => x.ChallengeId == challengeId
                        && x.UserId == userId
                        && x.ExpiresAt > now
                        && x.FailedAttempts < MaxAttempts)
                    .ExecuteDeleteAsync(cancellationToken);

            return consumed == 1;
        }

        var incremented = isSqlite
            ? await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE otp_challenges SET FailedAttempts = FailedAttempts + 1 WHERE ChallengeId = {challengeId} AND UserId = {userId} AND ExpiresAt > {now} AND FailedAttempts < {MaxAttempts}",
                cancellationToken)
            : await db.OtpChallenges
                .Where(x => x.ChallengeId == challengeId
                    && x.UserId == userId
                    && x.ExpiresAt > now
                    && x.FailedAttempts < MaxAttempts)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.FailedAttempts, x => x.FailedAttempts + 1),
                    cancellationToken);

        if (incremented == 0)
            return false;

        if (isSqlite)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM otp_challenges WHERE ChallengeId = {challengeId} AND UserId = {userId} AND FailedAttempts >= {MaxAttempts}",
                cancellationToken);
        }
        else
        {
            await db.OtpChallenges
                .Where(x => x.ChallengeId == challengeId
                    && x.UserId == userId
                    && x.FailedAttempts >= MaxAttempts)
                .ExecuteDeleteAsync(cancellationToken);
        }

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
