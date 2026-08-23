namespace HumanOS.Models.Capabilities.Studio;

/// <summary>What a <see cref="StructuredLearningIllustration"/> is used for
/// — mirrors <see cref="HumanOS.Models.Capabilities.Traditional.TraditionalIllustrationPurpose"/>'s
/// simplicity (this pipeline has no Hypothesis/Teaching illustrations today,
/// only DeepDive example cards).</summary>
public enum StructuredLearningIllustrationPurpose
{
    /// <summary>Document-level cover image.</summary>
    Cover = 3,

    /// <summary>One DeepDive example card's illustration.</summary>
    DeepDiveCard = 0,

    /// <summary>The Hypothesis step's illustration.</summary>
    Hypothesis = 1,

    /// <summary>The Teaching step's illustration — always present.</summary>
    Teaching = 2
}
