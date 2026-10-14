using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces.Repositories;
using RealFenixFailures.Infrastructure.Persistence;

namespace RealFenixFailures.Infrastructure.Repositories;

public class PresetRepository : IPresetRepository {
    private readonly RealFenixDbContext _dbContext;
    private readonly ILogger<PresetRepository> _logger;

    public PresetRepository(ILogger<PresetRepository> logger, RealFenixDbContext dbContext) {
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task AddAsync(IReadOnlyList<FailurePreset> presets, CancellationToken ct) {
        try {
            await _dbContext.AddRangeAsync(presets, ct);
            await _dbContext.SaveChangesAsync(ct);
        } catch (DbUpdateException ex) {
            _logger.LogError(ex, "Error guardando cambios: {Message}", ex.Message);
            _logger.LogError("Inner: {Inner}", ex.InnerException?.Message);
        }

    }

    public async Task<IReadOnlyList<FailurePreset>> GetAllAsync(PresetTypeEnum presetType, CancellationToken ct) {
        return await _dbContext.FailurePresets
            .Where(x => x.PresetType == presetType)
            .Include(x => x.PresetFailureDefinitions)
            .ThenInclude(x => x.FenixFailure)
            .ToListAsync(ct);
    }

    public async Task<bool> GetAnyAsync(PresetTypeEnum presetType, CancellationToken ct) {
        return await _dbContext.FailurePresets.AnyAsync(p => p.PresetType == presetType, ct);
    }

    public async Task<FailurePreset?> GetByIdAsync(int id, CancellationToken ct) {
        return await _dbContext.FailurePresets
            .Include(x => x.PresetFailureDefinitions)
            .ThenInclude(x => x.FenixFailure)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }
}
