using HumanOS.Models.People;

namespace HumanOS.Models.Motivations;

/// <summary>
/// A person's selection of a Motivation — simple existence join (no
/// lifecycle/Status, unlike <see cref="HumanOS.Models.Goals.PersonGoal"/>):
/// a motivation either resonates with the person right now or it doesn't.
/// Persisted via a full "replace the set" operation
/// (<see cref="Services.MotivationService.SetPersonMotivationsAsync"/>).
/// </summary>
public sealed class PersonMotivation
{
    public Guid PersonMotivationId { get; set; }

    public Guid PersonId { get; set; }

    public Guid MotivationId { get; set; }

    public DateTime CreatedDate { get; set; }

    public Person Person { get; set; } = null!;

    public Motivation Motivation { get; set; } = null!;
}
