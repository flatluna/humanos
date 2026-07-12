using HumanOS.Data;

namespace HumanOS.Services;

public sealed class GoalService
{
    private readonly HumanOsDbContext _dbContext;

    public GoalService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}
