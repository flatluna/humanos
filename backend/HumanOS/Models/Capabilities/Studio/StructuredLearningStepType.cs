namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// The pedagogical step types generated per section (Step 3) — independent
/// of Graph's <see cref="HumanOS.Models.Capabilities.Graph.ExperienceStepType"/>
/// (this pipeline never touches that enum/table). Fixed order:
/// Hypothesis -&gt; Teaching -&gt; DeepDive (optional) -&gt; Recall (optional) -&gt;
/// Production (optional) -&gt; Assessment.
/// </summary>
public enum StructuredLearningStepType
{
    Hypothesis = 0,
    Teaching = 1,
    DeepDive = 2,
    Recall = 3,
    Production = 4,
    Assessment = 5
}
