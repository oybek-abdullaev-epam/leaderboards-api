using Microsoft.EntityFrameworkCore;

namespace Leaderboards.MatchesApi.Data;

public class MatchesDbContext(DbContextOptions<MatchesDbContext> options) : DbContext(options)
{
    public DbSet<Match> Matches => Set<Match>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Match>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.WinnerId).IsRequired().HasMaxLength(256);
            e.Property(m => m.LoserId).IsRequired().HasMaxLength(256);
            e.Property(m => m.VenueName).IsRequired().HasMaxLength(256);
        });
    }
}
