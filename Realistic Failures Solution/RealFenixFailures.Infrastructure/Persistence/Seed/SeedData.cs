using Microsoft.EntityFrameworkCore;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Infrastructure.Persistence;

public static class SeedData {

    public static void Apply(ModelBuilder modelBuilder) {
        modelBuilder.Entity<PresetType>().HasData(
        Enum.GetValues(typeof(PresetTypeEnum))
            .Cast<PresetTypeEnum>()
            .Select(e => new PresetType {
                Id = (int)e,
                Description = e.ToString()
            })
        );

        SeedAircraftWearableSystems(modelBuilder);
    }

    private static void SeedAircraftWearableSystems(ModelBuilder modelBuilder) {
        // WearPerFlightHour (spec §3.2, rebalanceo 2026-10-25): %/h de vuelo por sistema. Motores/APU se desgastan
        // rápido, hidráulico/combustible/neumático por uso continuo, eléctrico/avionica moderado, estructura lento.
        // Subidos ~+15–50 % respecto del borrador inicial: el desgaste ahora solo cuenta horas de vuelo.
        var systems = new List<AircraftWearableSystem> {
            new() { Id = 1, AtaId = 21, Name = "Air Conditioning", ShortName = "Air", DisplayOrder = 1, WearPerFlightHour = 0.3 },
            new() { Id = 2, AtaId = 22, Name = "AutoFlight", ShortName = "AutoFlight", DisplayOrder = 2, WearPerFlightHour = 0.15 },
            new() { Id = 3, AtaId = 23, Name = "Communications", ShortName = "Communications", DisplayOrder = 3, WearPerFlightHour = 0.4 },
            new() { Id = 4, AtaId = 24, Name = "Electrical power", ShortName = "Elec", DisplayOrder = 4, WearPerFlightHour = 0.4 },
            new() { Id = 5, AtaId = 26, Name = "Fire protection", ShortName = "Fire", DisplayOrder = 5, WearPerFlightHour = 0.3 },
            new() { Id = 6, AtaId = 27, Name = "Flight controls", ShortName = "Flight controls", DisplayOrder = 6, WearPerFlightHour = 0.5 },
            new() { Id = 7, AtaId = 28, Name = "Fuel", ShortName = "Fuel", DisplayOrder = 7, WearPerFlightHour = 0.75 },
            new() { Id = 8, AtaId = 29, Name = "Hydraulic power", ShortName = "Hydraulic", DisplayOrder = 8, WearPerFlightHour = 0.75 },
            new() { Id = 9, AtaId = 30, Name = "Ice and rain protection", ShortName = "Ice/Rain", DisplayOrder = 9, WearPerFlightHour = 0.15 },
            new() { Id = 10, AtaId = 31, Name = "Indicating/Recording system", ShortName = "Indicating/Recording", DisplayOrder = 10, WearPerFlightHour = 0.4 },
            new() { Id = 11, AtaId = 32, Name = "Landing gear", ShortName = "Gear", DisplayOrder = 11, WearPerFlightHour = 0.3 },
            new() { Id = 12, AtaId = 34, Name = "Navigation", ShortName = "Navigation", DisplayOrder = 12, WearPerFlightHour = 0.4 },
            new() { Id = 13, AtaId = 35, Name = "Oxygen", ShortName = "Oxygen", DisplayOrder = 13, WearPerFlightHour = 0.15 },
            new() { Id = 14, AtaId = 36, Name = "Pneumatic", ShortName = "Pneumatic", DisplayOrder = 14, WearPerFlightHour = 0.75 },
            new() { Id = 15, AtaId = 46, Name = "Information systems", ShortName = "Information", DisplayOrder = 15, WearPerFlightHour = 0.4 },
            new() { Id = 16, AtaId = 49, Name = "APU", ShortName = "APU", DisplayOrder = 16, WearPerFlightHour = 0.5 },
            new() { Id = 17, AtaId = 52, Name = "Doors", ShortName = "Doors", DisplayOrder = 17, WearPerFlightHour = 0.15 },
            new() { Id = 18, AtaId = 70, Name = "Power plant", ShortName = "Engines", DisplayOrder = 18, WearPerFlightHour = 0.7 },
            new() { Id = 19, AtaId = 80, Name = "Start faults", ShortName = "Start Faults", DisplayOrder = 19, WearPerFlightHour = 1.0 }
        };

        modelBuilder.Entity<AircraftWearableSystem>().HasData(systems);
    }
}
