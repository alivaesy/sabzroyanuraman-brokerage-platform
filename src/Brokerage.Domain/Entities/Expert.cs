namespace Brokerage.Domain.Entities;

public class Expert
{
    public Guid Id { get; private set; }

    public string ExpertId { get; private set; }

    public string ExpertType { get; private set; }

    public bool IsActive { get; private set; }

    private Expert()
    {
        ExpertId = string.Empty;
        ExpertType = string.Empty;
    }

    public Expert(string expertId, string expertType)
    {
        Id = Guid.NewGuid();
        ExpertId = expertId;
        ExpertType = expertType;
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
