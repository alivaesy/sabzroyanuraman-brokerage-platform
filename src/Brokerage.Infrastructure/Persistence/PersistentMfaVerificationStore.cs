using Brokerage.Application.Authorization;

namespace Brokerage.Infrastructure.Persistence;

public sealed class PersistentMfaVerificationStore(BrokerageDbContext db) : IMfaVerificationStore
{
    private static readonly TimeSpan VerificationLifetime = TimeSpan.FromMinutes(15);

    public void MarkVerified(string userId, DateTimeOffset verifiedAt)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));
        if (userId.Length > 200)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID cannot exceed the persistence limit of 200 characters.");

        var record = db.MfaVerifications.Find(userId);
        if (record is null)
            db.MfaVerifications.Add(new MfaVerificationRecord { UserId = userId, VerifiedAt = verifiedAt });
        else
            record.VerifiedAt = verifiedAt;

        db.SaveChanges();
    }

    public bool IsVerified(string userId, DateTimeOffset now)
    {
        var record = db.MfaVerifications.Find(userId);
        if (record is null)
            return false;

        if (record.VerifiedAt > now || now - record.VerifiedAt > VerificationLifetime)
        {
            db.MfaVerifications.Remove(record);
            db.SaveChanges();
            return false;
        }

        return true;
    }
}
