using HumanOS.Data;

namespace HumanOS.Services;

public sealed class ProjectService
{
    private readonly HumanOsDbContext _dbContext;

    public ProjectService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}
