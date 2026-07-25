namespace HumanOS.Contracts.HumanState;

/// <summary>
/// Today's Human State check-in for a person — powers the "Estado Humano"
/// section of the "/today" dashboard. One row per person per calendar day
/// (UTC), self-reported by the person tapping a 1-5 rating per metric
/// (stored as 20/40/60/80/100).
/// </summary>
public sealed class HumanStateCheckInResponse
{
    public Guid PersonId { get; set; }

    /// <summary>Null when the person hasn't checked in yet today.</summary>
    public DateTime? RecordedAt { get; set; }

    public int? Energy { get; set; }

    public int? Focus { get; set; }

    public int? Purpose { get; set; }

    public int? Confidence { get; set; }

    /// <summary>Consecutive days (including today, once checked in) with a check-in.</summary>
    public int Streak { get; set; }

    /// <summary>Whether the person has already checked in today.</summary>
    public bool HasCheckedInToday { get; set; }
}

/// <summary>Request to record/update today's check-in. Each value is 0-100
/// (frontend collects a 1-5 rating and multiplies by 20).</summary>
public sealed class UpsertHumanStateRequest
{
    public int Energy { get; set; }

    public int Focus { get; set; }

    public int Purpose { get; set; }

    public int Confidence { get; set; }
}
