using Microsoft.EntityFrameworkCore;
public class ApplicationDbContext : DbContext
{
  public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
      : base(options)
  {
  }

  public DbSet<Note> Notes { get; set; }
  public DbSet<User> Users { get; set; }
}