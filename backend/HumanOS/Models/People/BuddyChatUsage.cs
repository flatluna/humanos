namespace HumanOS.Models.People;

/// <summary>
/// One completed Buddy TEXT chat turn's real token usage (2026-10-04) — the
/// SQL-backed counterpart to <see cref="BuddySessionUsage"/> (voice), so
/// Buddy's TEXT channel cost is no longer invisible/unmeasured. Recorded
/// server-side, straight from the model's own reported <c>Usage</c> on every
/// turn (see <see cref="HumanOS.Services.BuddyAgentService.AskAsync"/>) —
/// unlike voice (which the backend never sees the audio of, so duration is
/// client-reported), text usage is always known authoritatively server-side.
/// </summary>
public sealed class BuddyChatUsage
{
    public Guid BuddyChatUsageId { get; set; } = Guid.NewGuid();

    public Guid? PersonId { get; set; }

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
