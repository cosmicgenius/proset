using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace proset.Models {
   public class SqlContext : DbContext, IDataProtectionKeyContext {
      public SqlContext(DbContextOptions<SqlContext> options) : base(options) { }
      public DbSet<Models.User> Users { get; set; } = null!;
      public DbSet<Models.Game> Games { get; set; } = null!;
      public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;  
   }
}
