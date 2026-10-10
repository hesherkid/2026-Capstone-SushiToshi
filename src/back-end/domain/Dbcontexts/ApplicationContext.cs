using Microsoft.EntityFrameworkCore;
using back_end.domain.Entities;

namespace back_end.domain.DbContexts
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
        public DbSet<AuthActionToken> AuthActionTokens => Set<AuthActionToken>();
        public DbSet<Billing> Bills { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<DiningSession> DiningSessions { get; set; } = null!;
        public DbSet<Locations> Locations { get; set; } = null!;
        public DbSet<MenuItemAssignment> MenuItemAssignments { get; set; } = null!;
        public DbSet<Menu_Item> MenuItems { get; set; } = null!;
        public DbSet<MenuItemView> MenuItemViews { get; set; } = null!;
        public DbSet<MenuLocations> MenuLocations { get; set; } = null!;
        public DbSet<Menu> Menus { get; set; } = null!;
        public DbSet<MenuItemTag> MenuItemTags { get; set; } = null!;
        public DbSet<OrderItems> OrderItems { get; set; } = null!;
        public DbSet<ServiceRequest> ServiceRequests { get; set; } = null!;
        public DbSet<SessionOrder> SessionOrders { get; set; } = null!;
        public DbSet<SessionParticipant> SessionParticipants { get; set; } = null!;
        public DbSet<TableEntity> Tables { get; set; } = null!;
        public DbSet<TableGroup> TableGroups { get; set; } = null!;
        public DbSet<Tag> Tags { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // AuthActionToken for email verification and reset password
            modelBuilder.Entity<AuthActionToken>(entity =>
              {
                  entity.HasKey(x => x.Id);

                  entity.Property(x => x.TokenHash)
                      .HasMaxLength(64)
                      .IsRequired();

                  entity.HasIndex(x => x.TokenHash)
                      .IsUnique();

                  entity.HasOne(x => x.User)
                      .WithMany()
                      .HasForeignKey(x => x.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
              });
            // Composite keys for junction tables - REQUIRED since EF can't infer these
            modelBuilder.Entity<MenuItemTag>()
                .HasKey(mit => new { mit.Menu_item_id, mit.Tag_id });

            modelBuilder.Entity<MenuItemAssignment>()
                .HasKey(mia => new { mia.Menu_Id, mia.Item_Id });

            modelBuilder.Entity<MenuLocations>()
                .HasKey(ml => new { ml.Menu_Id, ml.Location_Id });

            modelBuilder.Entity<MenuItemView>(entity =>
            {
                entity.HasOne(v => v.MenuItem)
                    .WithMany()
                    .HasForeignKey(v => v.Item_Id)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(v => v.DiningSession)
                    .WithMany()
                    .HasForeignKey(v => v.Session_Id)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(v => new { v.Viewed_At, v.Location_Id })
                    .HasDatabaseName("IX_menu_item_view_viewed_at_location");

                entity.HasIndex(v => new { v.Session_Id, v.Item_Id })
                    .HasDatabaseName("IX_menu_item_view_session_item");
            });

            // DiningSession relationships
            modelBuilder.Entity<DiningSession>()
                .HasOne(ds => ds.Location)
                .WithMany(l => l.DiningSessions)
                .HasForeignKey(ds => ds.Location_Id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DiningSession>()
                .HasOne(ds => ds.Table)
                .WithMany(t => t.DiningSessions)
                .HasForeignKey(ds => ds.Table_Id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DiningSession>()
                .HasOne(ds => ds.TableGroup)
                .WithMany(tg => tg.DiningSessions)
                .HasForeignKey(ds => ds.TableGroup_Id)
                .OnDelete(DeleteBehavior.Restrict);

            // TableEntity relationships
            modelBuilder.Entity<TableEntity>()
                .HasOne(t => t.Location)
                .WithMany(l => l.Tables)
                .HasForeignKey(t => t.Location_Id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TableEntity>()
                .HasOne(t => t.TableGroup)
                .WithMany(tg => tg.Tables)
                .HasForeignKey(t => t.TableGroup_Id)
                .OnDelete(DeleteBehavior.SetNull);

            // TableGroup relationships
            modelBuilder.Entity<TableGroup>()
                .HasOne(tg => tg.Location)
                .WithMany(l => l.TableGroups)
                .HasForeignKey(tg => tg.Location_Id)
                .OnDelete(DeleteBehavior.Restrict);

            // DiningSession check constraint: has either Table_Id or TableGroup_Id, not both
            modelBuilder.Entity<DiningSession>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_DiningSession_TableAssignment",
                    "(Table_Id IS NOT NULL AND TableGroup_Id IS NULL) OR (Table_Id IS NULL AND TableGroup_Id IS NOT NULL) OR (Table_Id IS NULL AND TableGroup_Id IS NULL)"));

            // ServiceRequest relationships
            modelBuilder.Entity<ServiceRequest>()
                .HasOne(sr => sr.RequestedByUser)
                .WithMany(u => u.RequestedServices)
                .HasForeignKey(sr => sr.Request_By)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ServiceRequest>()
                .HasOne(sr => sr.ClaimedByUser)
                .WithMany(u => u.ClaimedServices)
                .HasForeignKey(sr => sr.Claimed_By)
                .OnDelete(DeleteBehavior.SetNull);
            base.OnModelCreating(modelBuilder);
        }
    }
}