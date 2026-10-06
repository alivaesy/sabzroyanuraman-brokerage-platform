using Brokerage.Application.Authorization;

namespace Brokerage.Infrastructure.Persistence;

public sealed class PersistentMfaVerificationStore(BrokerageDbContext db) : IMfaVerificationStore
{
    private static readonly TimeSpan VerificationLifetime = TimeSpan.FromMinutes(15);

    public void MarkVerified(string userId, DateTimeOffset verifiedAt)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));

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

        if (now - record.VerifiedAt > VerificationLifetime)
        {
            db.MfaVerifications.Remove(record);
            db.SaveChanges();
            return false;
        }

        return true;
    }
}
