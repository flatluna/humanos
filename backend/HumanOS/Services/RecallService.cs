using HumanOS.Data;

namespace HumanOS.Services;

public sealed class RecallService
{
    private readonly HumanOsDbContext _dbContext;

    public RecallService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}
