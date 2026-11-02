using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobTrack.API.Data;

public sealed class JobTrackDesignTimeFactory : IDesignTimeDbContextFactory<JobTrackDbContext>
{
    public JobTrackDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__JobTrack")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=JobTrackDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
        var options = new DbContextOptionsBuilder<JobTrackDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new JobTrackDbContext(options);
    }
}
