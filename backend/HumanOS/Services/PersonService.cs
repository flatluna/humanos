using HumanOS.Data;

namespace HumanOS.Services;

public sealed class PersonService
{
    private readonly HumanOsDbContext _dbContext;

    public PersonService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}
