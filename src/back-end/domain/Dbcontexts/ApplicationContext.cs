using back_end.domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace back_end.domain.DbContexts
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
        public DbSet<AuthActionToken> AuthActionTokens => Set<AuthActionToken>();
        public DbSet<Billing> Bills { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<FavouriteItem> FavouriteItems { get; set; } = null!;
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
        public DbSet<Holiday> Holidays { get; set; } = null!;
        public DbSet<PricingProfile> PricingProfiles { get; set; } = null!;
        public DbSet<PricingRate> PricingRates { get; set; } = null!;
        public DbSet<StaffLocation> StaffLocations { get; set; } = null!;


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

            modelBuilder.Entity<MenuItemAssignment>(entity =>
            {
                entity.HasKey(a => a.Assignment_Id);
                entity.HasIndex(a => a.Menu_Id).HasDatabaseName("IX_menu_item_assignment_menu_id");


                // MariaDB treats NULLs as distinct in unique indexes, so index a computed
                // column instead; this allows only one default row per menu + item
                entity.Property(a => a.Location_Key)
                    .HasComputedColumnSql("IFNULL(location_id, 0)", stored: true);

                entity.HasIndex(a => new { a.Menu_Id, a.Item_Id, a.Location_Key })
                    .IsUnique()
                    .HasDatabaseName("UX_assignment_menu_item_location");

                entity.HasOne(a => a.Location)
                    .WithMany(l => l.MenuItemAssignments)
                    .HasForeignKey(a => a.Location_Id)
                    .OnDelete(DeleteBehavior.Cascade);   // deleting a location removes its overrides
            });


            modelBuilder.Entity<MenuLocations>()
                .HasKey(ml => new { ml.Menu_Id, ml.Location_Id });

            modelBuilder.Entity<StaffLocation>(entity =>
            {
                entity.HasKey(sl => new { sl.User_Id, sl.Location_Id });

                entity.HasOne(sl => sl.User)
                    .WithMany(u => u.AssignedLocations)
                    .HasForeignKey(sl => sl.User_Id)
                    .OnDelete(DeleteBehavior.Cascade);

                // Deleting a location takes it off everyone's assigned list
                entity.HasOne(sl => sl.Location)
                    .WithMany()
                    .HasForeignKey(sl => sl.Location_Id)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Deleting a customer or a menu item takes it off favourites
            modelBuilder.Entity<FavouriteItem>(entity =>
            {
                entity.HasKey(f => new { f.User_Id, f.Item_Id });

                entity.HasOne(f => f.User)
                    .WithMany()
                    .HasForeignKey(f => f.User_Id)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(f => f.MenuItem)
                    .WithMany()
                    .HasForeignKey(f => f.Item_Id)
                    .OnDelete(DeleteBehavior.Cascade);
            });

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
                .HasOne(ds => ds.Menu)
                .WithMany(m => m.DiningSessions)
                .HasForeignKey(ds => ds.Menu_Id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderItems>()
                .HasOne(oi => oi.Menu)
                .WithMany(m => m.OrderItems)
                .HasForeignKey(oi => oi.Menu_Id)
                .OnDelete(DeleteBehavior.Restrict);

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

            // Existing and new locations default to Alberta
            modelBuilder.Entity<Locations>()
                .Property(l => l.Province_Code)
                .HasDefaultValue("AB");

            // Pricing: one default profile (location_id NULL) plus at most one per location
            modelBuilder.Entity<PricingProfile>(entity =>
            {
                // Same trick as MenuItemAssignment: MariaDB allows many NULLs in a unique index
                entity.Property(p => p.Location_Key)
                    .HasComputedColumnSql("IFNULL(location_id, 0)", stored: true);

                entity.HasIndex(p => p.Location_Key)
                    .IsUnique()
                    .HasDatabaseName("UX_pricing_profile_location");

                entity.HasOne(p => p.Location)
                    .WithOne(l => l.PricingProfile)
                    .HasForeignKey<PricingProfile>(p => p.Location_Id)
                    .OnDelete(DeleteBehavior.Cascade);   // deleting a location removes its pricing

                entity.Property(p => p.Weekend_Days)
                    .HasConversion(WeekendDaysConverter, WeekendDaysComparer);
            });

            modelBuilder.Entity<PricingRate>(entity =>
            {
                entity.HasIndex(r => new { r.Pricing_Profile_Id, r.Day_Type })
                    .IsUnique()
                    .HasDatabaseName("UX_pricing_rate_profile_day_type");

                entity.HasOne(r => r.PricingProfile)
                    .WithMany(p => p.Rates)
                    .HasForeignKey(r => r.Pricing_Profile_Id)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Holidays: one row per province per date; re-importing never duplicates
            modelBuilder.Entity<Holiday>(entity =>
            {
                entity.HasIndex(h => new { h.Province_Code, h.Date })
                    .IsUnique()
                    .HasDatabaseName("UX_holiday_province_date");

                entity.HasIndex(h => new { h.Province_Code, h.Status, h.Date })
                    .HasDatabaseName("IX_holiday_province_status_date");

                entity.HasOne(h => h.ReviewedByUser)
                    .WithMany()
                    .HasForeignKey(h => h.Reviewed_By)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            ApplyUtcDateTimes(modelBuilder);
            base.OnModelCreating(modelBuilder);
        }

        // Every timestamp is stored in UTC, but MySQL hands DateTimes back with no time zone,
        // so the API sent "2026-10-18T04:25:59" and browsers read it as local time (6-7 hours off in Edmonton).
        // Reading marks values as UTC so they're sent as "...Z"; saving converts any local time
        // (e.g. a DateTime.Now default) to UTC so stored values stay consistent.
        private static readonly ValueConverter<DateTime, DateTime> UtcDateTimeConverter = new(
            toDb => toDb.Kind == DateTimeKind.Local ? toDb.ToUniversalTime() : toDb,
            fromDb => DateTime.SpecifyKind(fromDb, DateTimeKind.Utc));

        // Weekend days are stored as "Friday,Saturday,Sunday"
        private static readonly ValueConverter<List<DayOfWeek>, string> WeekendDaysConverter = new(
            days => string.Join(",", days),
            value => value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                          .Select(Enum.Parse<DayOfWeek>)
                          .ToList());

        // Lets EF notice when a day is added to or removed from the list
        private static readonly ValueComparer<List<DayOfWeek>> WeekendDaysComparer = new(
            (a, b) => a!.SequenceEqual(b!),
            days => days.Aggregate(0, (hash, d) => HashCode.Combine(hash, d)),
            days => days.ToList());


        private static void ApplyUtcDateTimes(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    // Nullable DateTimes get the same converter; EF skips it for nulls
                    if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(UtcDateTimeConverter);
                    }
                }
            }
        }
    }
}