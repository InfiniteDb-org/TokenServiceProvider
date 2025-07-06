using Microsoft.EntityFrameworkCore;
using TokenService.Api.Models;

namespace TokenService.Api.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    
    public DbSet<RefreshToken> RefreshTokens { get; set; }
}