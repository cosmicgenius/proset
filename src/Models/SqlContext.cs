using Microsoft.EntityFrameworkCore;

namespace proset.Models {
   public class SqlContext : DbContext {
      public SqlContext(DbContextOptions<SqlContext> options) : base(options) { }
      public DbSet<Models.User>? Users { get; set; }
      public DbSet<Models.Game>? Games { get; set; }
   }
}
