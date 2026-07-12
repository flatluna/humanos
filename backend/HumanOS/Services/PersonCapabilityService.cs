using HumanOS.Data;

namespace HumanOS.Services;

public sealed class PersonCapabilityService
{
    private readonly HumanOsDbContext _dbContext;

    public PersonCapabilityService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}
