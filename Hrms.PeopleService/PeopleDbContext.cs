using Hrms.PeopleService.Models;
using Microsoft.EntityFrameworkCore;

namespace Hrms.PeopleService;

public class PeopleDbContext : DbContext
{
    private readonly IConfiguration _configuration; 
    
    public PeopleDbContext(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public DbSet<Employee> Employees { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>()
            .Property(e => e.DateOfBirth)
            .HasColumnType("date");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql(_configuration.GetConnectionString("PeopleDB"));
        }
    }
}