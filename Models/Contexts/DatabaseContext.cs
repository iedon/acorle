using System;
using Microsoft.EntityFrameworkCore;


#nullable disable

namespace Acorle.Models.Contexts
{
    public partial class DatabaseContext : DbContext
    {
        // We use context pooling.
        // public DatabaseContext() {}

        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) {}

        public virtual DbSet<ServiceConfig> Configs { get; set; }
        public virtual DbSet<LoadBalancer> LoadBalancers { get; set; }
        public virtual DbSet<Service> Services { get; set; }
        public virtual DbSet<Zone> Zones { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {}

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_0900_ai_ci");

            modelBuilder.Entity<ServiceConfig>(entity =>
            {
                entity.HasKey(e => new { e.Zone, e.Key })
                    .HasName("PRIMARY")
                    .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

                entity.ToTable("config");

                entity.UseCollation("utf8mb4_general_ci");

                entity.HasIndex(e => new { e.Zone, e.Key }, "idx_config")
                    .IsUnique();

                entity.Property(e => e.Zone)
                    .HasColumnName("zone")
                    .HasComment("Zone Key")
                    .UseCollation("utf8mb4_0900_ai_ci");

                entity.Property(e => e.Key)
                    .HasColumnName("key")
                    .HasComment("Configuration Key")
                    .UseCollation("utf8mb4_0900_ai_ci");

                entity.Property(e => e.Context)
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("context")
                    .HasComment("Configuration")
                    .UseCollation("utf8mb4_0900_ai_ci");

                entity.Property(e => e.Hash)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("hash")
                    .HasComment("Configuration Hash")
                    .UseCollation("utf8mb4_0900_ai_ci");

                entity.Property(e => e.LastModified)
                    .HasMaxLength(6)
                    .HasColumnName("last_modified")
                    .HasComment("Configuration Last Modified Time")
                    .HasConversion(e => e.ToUniversalTime(), e => DateTime.SpecifyKind(e, DateTimeKind.Utc));
            });

            modelBuilder.Entity<LoadBalancer>(entity =>
            {
                entity.HasKey(e => new { e.Zone, e.Service })
                    .HasName("PRIMARY")
                    .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

                entity.ToTable("load_balancer");

                entity.UseCollation("utf8mb4_general_ci");

                entity.HasIndex(e => new { e.Zone, e.Service }, "idx_load_balancer")
                    .IsUnique();

                entity.Property(e => e.Zone)
                    .HasColumnName("zone")
                    .HasComment("Zone Key")
                    .UseCollation("utf8mb4_0900_ai_ci");

                entity.Property(e => e.Service)
                    .HasColumnName("service")
                    .HasComment("Service Key")
                    .UseCollation("utf8mb4_0900_ai_ci");

                entity.Property(e => e.Type)
                    .HasColumnType("int(10) unsigned")
                    .HasColumnName("type")
                    .HasComment("Load Balancer Type");
            });

            modelBuilder.Entity<Service>(entity =>
            {
                entity.HasKey(e => new { e.Zone, e.Hash })
                    .HasName("PRIMARY")
                    .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

                entity.ToTable("service");

                entity.HasIndex(e => new { e.Zone, e.Hash }, "idx_service")
                    .IsUnique();

                entity.HasIndex(e => e.Zone, "idx_service_zone");

                entity.HasIndex(e => new { e.Zone, e.Key }, "idx_service_zone_key");

                entity.Property(e => e.Zone)
                    .HasColumnName("zone")
                    .HasComment("Zone Key");

                entity.Property(e => e.Hash)
                    .HasColumnName("hash")
                    .HasComment("Service Entry Hash(hash of key and url)");

                entity.Property(e => e.AddedTime)
                    .HasMaxLength(6)
                    .HasColumnName("added_time")
                    .HasComment("Service Added Time")
                    .HasConversion(e => e.ToUniversalTime(), e => DateTime.SpecifyKind(e, DateTimeKind.Utc));

                entity.Property(e => e.ExpireTime)
                    .HasMaxLength(6)
                    .HasColumnName("expire_time")
                    .HasComment("Service Expire Time")
                    .HasConversion(e => e.ToUniversalTime(), e => DateTime.SpecifyKind(e, DateTimeKind.Utc));

                entity.Property(e => e.IsPrivate)
                    .HasColumnName("is_private")
                    .HasComment("Service Is Private");

                entity.Property(e => e.Key)
                    .IsRequired()
                    .HasColumnName("key")
                    .HasComment("Service Key");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("name")
                    .HasComment("Service Display Name");

                entity.Property(e => e.Url)
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("url")
                    .HasComment("Service URL");

                entity.Property(e => e.Weight)
                    .HasColumnType("int(11)")
                    .HasColumnName("weight")
                    .HasComment("Service Load Balance Weight");
            });

            modelBuilder.Entity<Zone>(entity =>
            {
                entity.HasKey(e => e.Key)
                    .HasName("PRIMARY");

                entity.ToTable("zone");

                entity.UseCollation("utf8mb4_general_ci");

                entity.HasIndex(e => e.Key, "idx_zone_key")
                    .IsUnique();

                entity.HasIndex(e => e.Name, "idx_zone_name");

                entity.Property(e => e.Key)
                    .HasColumnName("key")
                    .HasComment("Zone Key")
                    .UseCollation("utf8mb4_0900_ai_ci");

                entity.Property(e => e.Description)
                    .HasColumnType("text")
                    .HasColumnName("description")
                    .HasComment("Zone Description")
                    .UseCollation("utf8mb4_0900_ai_ci");

                entity.Property(e => e.LogUserRequest)
                    .HasColumnName("log_user_request")
                    .HasComment("Zone Log User Request");

                entity.Property(e => e.MaxServices)
                    .HasColumnType("int(10) unsigned")
                    .HasColumnName("max_services")
                    .HasComment("Zone Max Allowed Services");

                entity.Property(e => e.Name)
                    .HasColumnName("name")
                    .HasComment("Zone Friendly Name")
                    .UseCollation("utf8mb4_0900_ai_ci");

                entity.Property(e => e.RegIntervalSeconds)
                    .HasColumnType("int(10) unsigned")
                    .HasColumnName("reg_interval_seconds")
                    .HasComment("Zone Service Registration Interval Seconds");

                entity.Property(e => e.RpcTimeoutSeconds)
                    .HasColumnType("int(10) unsigned")
                    .HasColumnName("rpc_timeout_seconds")
                    .HasComment("Zone RPC Request Timeout Seconds");

                entity.Property(e => e.Secret)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("secret")
                    .HasComment("Zone Registration Secret")
                    .UseCollation("utf8mb4_0900_ai_ci");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
