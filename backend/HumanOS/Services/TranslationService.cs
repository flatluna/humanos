using HumanOS.Data;

namespace HumanOS.Services;

public sealed class TranslationService
{
    private readonly HumanOsDbContext _dbContext;

    public TranslationService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}
