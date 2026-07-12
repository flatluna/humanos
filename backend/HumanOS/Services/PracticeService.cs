using HumanOS.Data;

namespace HumanOS.Services;

public sealed class PracticeService
{
    private readonly HumanOsDbContext _dbContext;

    public PracticeService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}
