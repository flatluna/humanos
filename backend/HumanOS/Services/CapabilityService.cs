using HumanOS.Data;

namespace HumanOS.Services;

public sealed class CapabilityService
{
    private readonly HumanOsDbContext _dbContext;

    public CapabilityService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}
