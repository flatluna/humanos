namespace HumanOS.Models.People;

/// <summary>
/// One completed Buddy (<see cref="HumanOS.AzureFunctions.Api.BuddyVoiceSessionFunction"/>)
/// Realtime voice session's duration (2026-10-01) — the SQL backing store for the
/// time/cost-by-Buddy-usage tenant dashboard card. Buddy connects browser-to-Azure
/// directly over WebRTC (see RealtimeVoiceSessionService's ephemeral-token model), so
/// the backend never sees the live audio stream or an Azure-reported usage total —
/// the browser itself measures wall-clock call duration and reports it once, when the
/// call ends (see BuddyVoiceModal's teardown + PostBuddySessionUsageFunction), the same
/// "client reports, server never trusts amounts it can't verify independently" pattern
/// used nowhere else yet in this codebase since every other cost source (LLM token
/// usage, image/video generation) is itself server-side. TenantId is deliberately NOT
/// duplicated here — it's resolved via <see cref="Person.TenantId"/> at query time
/// (see TenantBuddyUsageService), same as CapabilityGenerationUsage resolves its
/// tenant via Capability.OwnerTenantId.
/// </summary>
public sealed class BuddySessionUsage
{
    public Guid BuddySessionUsageId { get; set; } = Guid.NewGuid();

    /// <summary>FK: the employee who had this Buddy conversation. Null if the
    /// caller couldn't be resolved from the request's identity headers (the
    /// usage is then excluded from any per-tenant rollup).</summary>
    public Guid? PersonId { get; set; }

    /// <summary>Wall-clock call duration, measured client-side from WebRTC connect
    /// to teardown.</summary>
    public int DurationSeconds { get; set; }

    public DateTime CreatedDate { get; set; }
}
