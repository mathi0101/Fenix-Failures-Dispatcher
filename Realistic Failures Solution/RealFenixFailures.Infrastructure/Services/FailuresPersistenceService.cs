using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Interfaces.Repositories;
using RealFenixFailures.Domain.Services;

namespace RealFenixFailures.Infrastructure.Services;

public class FailuresPersistenceService : IFailuresPersistenceService {
    private readonly IFenixFailureDefinitionRepository _fenixFailuresRepository;
    private readonly IFenixStreamFailuresReaderService _fenixFailuresReader;

    public FailuresPersistenceService(IFenixFailureDefinitionRepository fenixFailureDefinitionRepository, IFenixStreamFailuresReaderService fenixJsonReaderService) {
        _fenixFailuresRepository = fenixFailureDefinitionRepository;
        _fenixFailuresReader = fenixJsonReaderService;
    }

    public async Task LoadInitialFailuresAsync(CancellationToken ct) {
        var hasData = await _fenixFailuresRepository.HasAnyData(ct);
        if (hasData) return;
        using Stream stream = EmbeddedJsonLoader.LoadFromEmbeddedJson(EmbeddedResourceNames.FailuresCatalog) ?? throw new ArgumentException("Error al cargar fallas iniciales");
        var failures = await _fenixFailuresReader.ReadAsync(stream, ct);

        if (failures.MajorGroups.Count == 0)
            throw new ArgumentException("Error al cargar fallas iniciales");

        var systems = failures.MajorGroups.Select(
            major => {
                var system = new FenixFailureSystem {
                    Name = major.Title,
                    ShortName = major.ShortTitle
                };
                system.FailureGroups = major.SystemGroups.Select(
                    group => {
                        var failureGroup = new FenixFailureGroup {
                            Name = group.Name,
                            System = system
                        };
                        failureGroup.FailureDefinitions = group.Failures.Select(
                            failure => new FenixFailureDefinition {
                                FenixFailureId = failure.FenixId,
                                Name = failure.Description,
                                Severity = failure.Severity,
                                CanBeRepaired = failure.Repairable,
                                Group = failureGroup
                            }).ToList();
                        return failureGroup;
                    }).ToList();
                return system;
            }
        ).ToList();

        await _fenixFailuresRepository.LoadNewFailuresAsync(systems, ct);
    }
}
