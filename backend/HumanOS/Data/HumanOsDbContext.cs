using HumanOS.Models.Assessments;
using HumanOS.Models.Capabilities;
using HumanOS.Models.Evidence;
using HumanOS.Models.Goals;
using HumanOS.Models.Localization;
using HumanOS.Models.People;
using HumanOS.Models.Practice;
using HumanOS.Models.Projects;
using HumanOS.Models.Recall;
using HumanOS.Models.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Data;

public sealed class HumanOsDbContext : DbContext
{
    public HumanOsDbContext(
        DbContextOptions<HumanOsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Person> People => Set<Person>();

    public DbSet<PersonProfile> PersonProfiles => Set<PersonProfile>();

    public DbSet<HumanProfile> HumanProfiles => Set<HumanProfile>();

    public DbSet<Language> Languages => Set<Language>();

    public DbSet<CapabilityDomain> CapabilityDomains =>
        Set<CapabilityDomain>();

    public DbSet<CapabilityDomainTranslation>
        CapabilityDomainTranslations =>
        Set<CapabilityDomainTranslation>();

    public DbSet<Capability> Capabilities => Set<Capability>();

    public DbSet<CapabilityTranslation> CapabilityTranslations =>
        Set<CapabilityTranslation>();

    public DbSet<PersonCapability> PersonCapabilities =>
        Set<PersonCapability>();

    public DbSet<CapabilityEvidence> CapabilityEvidence =>
        Set<CapabilityEvidence>();

    public DbSet<CapabilityPractice> CapabilityPractices =>
        Set<CapabilityPractice>();

    public DbSet<RecallAttempt> RecallAttempts =>
        Set<RecallAttempt>();

    public DbSet<Goal> Goals => Set<Goal>();

    public DbSet<GoalTranslation> GoalTranslations =>
        Set<GoalTranslation>();

    public DbSet<GoalCapability> GoalCapabilities =>
        Set<GoalCapability>();

    public DbSet<PersonGoal> PersonGoals => Set<PersonGoal>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectTranslation> ProjectTranslations =>
        Set<ProjectTranslation>();

    public DbSet<PersonProject> PersonProjects =>
        Set<PersonProject>();

    public DbSet<Evidence> Evidence => Set<Evidence>();

    public DbSet<Assessment> Assessments => Set<Assessment>();

    public DbSet<AssessmentAttempt> AssessmentAttempts =>
        Set<AssessmentAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(HumanOsDbContext).Assembly);
    }
}
