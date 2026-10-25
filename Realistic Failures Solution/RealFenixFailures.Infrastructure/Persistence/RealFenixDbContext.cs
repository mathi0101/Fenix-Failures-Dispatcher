using Microsoft.EntityFrameworkCore;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Infrastructure.Persistence;

public class RealFenixDbContext : DbContext {
    public RealFenixDbContext(DbContextOptions<RealFenixDbContext> options) : base(options) {
    }

    public DbSet<PresetType> FailurePresetTypes => Set<PresetType>();
    public DbSet<FenixFailureDefinition> FenixFailureDefinitions => Set<FenixFailureDefinition>();
    public DbSet<FenixFailureGroup> FenixFailureGroups => Set<FenixFailureGroup>();
    public DbSet<FenixFailureSystem> FenixFailureSystems => Set<FenixFailureSystem>();
    public DbSet<FailurePreset> FailurePresets => Set<FailurePreset>();
    public DbSet<PresetFailureDefinition> PresetFailureDefinitions => Set<PresetFailureDefinition>();
    public DbSet<FlightSession> FlightSessions => Set<FlightSession>();
    public DbSet<TriggeredFailure> TriggeredFailures => Set<TriggeredFailure>();
    public DbSet<SessionEventLog> SessionEventLogs => Set<SessionEventLog>();
    public DbSet<SessionSystemWearDelta> SessionSystemWearDeltas => Set<SessionSystemWearDelta>();
    public DbSet<UserAircraft> UserAircrafts => Set<UserAircraft>();
    public DbSet<AircraftWearableSystem> AircraftWearableSystems => Set<AircraftWearableSystem>();
    public DbSet<AircraftSystemWear> AircraftSystemWears => Set<AircraftSystemWear>();
    public DbSet<FlightPlan> FlightPlans => Set<FlightPlan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {

        #region EnumsTables

        modelBuilder.Entity<PresetType>(entity => {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Description).HasMaxLength(200).IsRequired();
        });

        #endregion

        #region Aircraft & Wear Systems

        modelBuilder.Entity<UserAircraft>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Registration).HasMaxLength(10).IsRequired();
            entity.HasIndex(x => x.Registration).IsUnique();
            entity.Property(x => x.IcaoTypeCode).HasMaxLength(10).IsRequired();
            entity.HasMany(x => x.SystemWears)
                  .WithOne(w => w.UserAircraft)
                  .HasForeignKey(w => w.UserAircraftId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.FlightSessions)
                  .WithOne(f => f.UserAircraft)
                  .HasForeignKey(f => f.UserAircraftId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AircraftWearableSystem>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AtaId).IsRequired().HasDefaultValue(0);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ShortName).HasMaxLength(20).IsRequired();
            entity.HasMany(x => x.Wears)
                  .WithOne(w => w.WearableSystem)
                  .HasForeignKey(w => w.WearableSystemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AircraftSystemWear>(entity => {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserAircraftId, x.WearableSystemId }).IsUnique();
            entity.HasOne(x => x.UserAircraft)
                  .WithMany(a => a.SystemWears)
                  .HasForeignKey(x => x.UserAircraftId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WearableSystem)
                  .WithMany(s => s.Wears)
                  .HasForeignKey(x => x.WearableSystemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        #endregion

        #region Fenix Failures

        modelBuilder.Entity<FenixFailureDefinition>(entity => {
            entity.HasKey(x => x.FenixFailureId);
            entity.Property(x => x.FenixFailureId).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CanBeTemporary).HasDefaultValue(false);
            entity.Property(x => x.CanBeRepaired).HasDefaultValue(false);
            entity.HasOne(x => x.Group)
                  .WithMany(g => g.FailureDefinitions)
                  .HasForeignKey(x => x.GroupId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .IsRequired();
        });

        modelBuilder.Entity<FenixFailureGroup>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasOne(x => x.System)
                  .WithMany(g => g.FailureGroups)
                  .HasForeignKey(x => x.SystemId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .IsRequired();
        });

        modelBuilder.Entity<FenixFailureSystem>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ShortName).HasMaxLength(50).IsRequired();
            entity.Property(x => x.AtaId).IsRequired().HasDefaultValue(0);
        });

        #endregion

        #region Presets

        modelBuilder.Entity<FailurePreset>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(600).IsRequired();
            entity.Property(x => x.TriggerDescription).HasMaxLength(600).IsRequired();
            entity.Ignore(x => x.PresetType);
            entity.Property(x => x.PresetTypeId)
                  .HasColumnName("PresetType")
                  .IsRequired();
            entity.HasOne<PresetType>()
                  .WithMany()
                  .HasForeignKey(x => x.PresetTypeId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .IsRequired();
            entity.HasMany(x => x.PresetFailureDefinitions)
                  .WithOne(pf => pf.Preset)
                  .HasForeignKey(pf => pf.PresetId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PresetFailureDefinition>(entity => {
            entity.HasKey(p => new { p.PresetId, p.FenixFailureId });
            entity.Property(x => x.FenixFailureId).HasMaxLength(180).IsRequired();
            entity.HasIndex(x => x.FenixFailureId);
            entity.Property(x => x.ProbabilityGroup);
            entity.Property(x => x.Probability).IsRequired();
            entity.Property(x => x.Ias).HasMaxLength(10);
            entity.Property(x => x.Above_Altitude).HasMaxLength(10);
            entity.Property(x => x.Below_Altitude).HasMaxLength(10);
            entity.Property(x => x.Time).HasMaxLength(10);
            entity.Property(x => x.AfterEvent).HasMaxLength(50);
            entity.Property(x => x.AfterEventSeconds).HasMaxLength(50);
            entity.HasOne(x => x.FenixFailure)
                  .WithMany(x => x.PresetFailureDefinitions)
                  .HasForeignKey(x => x.FenixFailureId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        #endregion

        modelBuilder.Entity<FlightSession>(entity => {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.UserAircraft).WithMany(a => a.FlightSessions).HasForeignKey(x => x.UserAircraftId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.FlightPlan).WithMany(p => p.Sessions).HasForeignKey(x => x.FlightPlanId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.EventLogs)
                  .WithOne(e => e.FlightSession)
                  .HasForeignKey(e => e.FlightSessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FlightPlan>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OriginIcao).HasMaxLength(10).IsRequired();
            entity.Property(x => x.DestinationIcao).HasMaxLength(10).IsRequired();
            entity.Property(x => x.AircraftIcaoType).HasMaxLength(10).IsRequired();
            entity.Property(x => x.FlightNumber).HasMaxLength(20);
            entity.Property(x => x.Callsign).HasMaxLength(20);
            entity.Property(x => x.RouteText).HasMaxLength(2000);
            entity.Property(x => x.RawData).HasColumnType("TEXT");
            entity.Property(x => x.Source).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ValidationMessage).HasMaxLength(500);
        });

        modelBuilder.Entity<TriggeredFailure>(entity => {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.FlightSession).WithMany(x => x.TriggeredFailures).HasForeignKey(x => x.FlightSessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.FenixFailure).WithMany().HasForeignKey(x => x.FenixFailureId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.State).HasDefaultValue(FailureLifecycleState.Sent).HasSentinel(FailureLifecycleState.Sent);
            entity.Property(x => x.CanBeTemporary).HasDefaultValue(false);
            entity.Property(x => x.FailureName).HasMaxLength(200);
            entity.Property(x => x.PresetName).HasMaxLength(120);
            entity.Property(x => x.Ias).HasMaxLength(10);
            entity.Property(x => x.AboveAltitude).HasMaxLength(10);
            entity.Property(x => x.BelowAltitude).HasMaxLength(10);
            entity.Property(x => x.Time).HasMaxLength(10);
            entity.Property(x => x.AfterEvent).HasMaxLength(50);
            entity.Property(x => x.AfterEventSeconds).HasMaxLength(50);
        });

        modelBuilder.Entity<SessionEventLog>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventCode).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => new { x.FlightSessionId, x.OccurredAt });
            entity.HasOne(x => x.FlightSession)
                  .WithMany(s => s.EventLogs)
                  .HasForeignKey(x => x.FlightSessionId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.TriggeredFailure)
                  .WithMany()
                  .HasForeignKey(x => x.TriggeredFailureId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        SeedData.Apply(modelBuilder);
    }
}
