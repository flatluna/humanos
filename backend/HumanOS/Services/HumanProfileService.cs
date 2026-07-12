using HumanOS.Data;

namespace HumanOS.Services;

public sealed class HumanProfileService
{
    private readonly HumanOsDbContext _dbContext;

    public HumanProfileService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}
