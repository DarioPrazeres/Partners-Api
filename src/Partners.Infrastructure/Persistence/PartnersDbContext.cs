using Microsoft.EntityFrameworkCore;
using Partners.Core.Domain;

namespace Partners.Infrastructure.Persistence;

public class PartnersDbContext(DbContextOptions<PartnersDbContext> options) : DbContext(options)
{
    public DbSet<Partner> Partners => Set<Partner>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PartnerConfiguration());
    }
}
