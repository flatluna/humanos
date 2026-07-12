using HumanOS.Data;

namespace HumanOS.Services;

public sealed class AssessmentService
{
    private readonly HumanOsDbContext _dbContext;

    public AssessmentService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}
