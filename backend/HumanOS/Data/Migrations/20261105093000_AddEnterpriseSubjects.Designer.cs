using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations;

[DbContext(typeof(HumanOS.Data.HumanOsDbContext))]
[Migration("20261105093000_AddEnterpriseSubjects")]
partial class AddEnterpriseSubjects
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
    }
}
