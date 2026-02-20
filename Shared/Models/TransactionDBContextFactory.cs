using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace Shared.Models;

public class TransactionDBContextFactory : IDesignTimeDbContextFactory<TransactionDBContext>
{
    public TransactionDBContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TransactionDBContext>();
        
        // Hardcoded connection string for design-time/migrations only. In production, this should come from configuration/environment variables and injected via a vault or similar secret storage.
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=transactions;Username=myuser;Password=mypassword");
        
        return new TransactionDBContext(optionsBuilder.Options);
    }
}