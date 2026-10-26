using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Services;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Interfaces.Repositories;
using RealFenixFailures.Domain.Services;

namespace RealFenixFailures.TestUnits.Services;

public class SimbriefOfpServiceTests {
    private static RealisticModeSettings ModeSettings() => new() {
        EvaluationIntervalSeconds = 30,
        PreparedQueueLimit = 5,
        GroundFailureEnabled = true,
        ThinkingLinesCap = 50,
        Ofp = new OfpWindowSettings { PreStartWindowHours = 2, PostArrivalWindowHours = 2 },
        Wear = new WearSettings { ThresholdNoticePct = 80 },
    };

    private static FlightPlan ValidPlan() {
        var off = DateTime.UtcNow.AddHours(-1);
        return new FlightPlan {
            AircraftIcaoType = "A320",
            OriginIcao = "LFBD",
            DestinationIcao = "LIRF",
            FlightNumber = "EZY123",
            EstimatedDurationMinutes = 95,
            OffBlockUtc = off,
            EstimatedArrivalUtc = off.AddHours(2),
            Source = "SimBrief",
            DownloadedAtUtc = DateTime.UtcNow,
            ValidFromUtc = off,
            ValidUntilUtc = off.AddHours(2),
            ValidationState = ValidationState.Pending,
        };
    }

    private static SimbriefOfpService CreateService(
        Mock<IOfpFetcher>? fetcherMock = null,
        Mock<ISimBriefOfpReader>? readerMock = null,
        Mock<IFlightPlanRepository>? repoMock = null) {
        fetcherMock ??= new Mock<IOfpFetcher>();
        readerMock ??= new Mock<ISimBriefOfpReader>();
        repoMock ??= new Mock<IFlightPlanRepository>();

        var settings = Options.Create(new FailureEngineSettings { RealisticMode = ModeSettings() });
        return new SimbriefOfpService(
            fetcherMock.Object,
            readerMock.Object,
            repoMock.Object,
            settings,
            NullLogger<SimbriefOfpService>.Instance);
    }

    [Fact]
    public async Task DownloadAsync_InWindow_IsValid_AndPersists() {
        var fetcherMock = new Mock<IOfpFetcher>();
        var repoMock = new Mock<IFlightPlanRepository>();
        var plan = ValidPlan();

        fetcherMock.Setup(f => f.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new MemoryStream());

        // El reader devuelve el plan parseado.
        var readerMock = new Mock<ISimBriefOfpReader>();
        readerMock.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(plan);
        var service = CreateService(fetcherMock, readerMock, repoMock);

        var dto = await service.DownloadAsync("A320", "12345", CancellationToken.None);

        Assert.Equal(ValidationState.Valid, dto.ValidationState);
        Assert.True(dto.IsUsable);
        Assert.Equal("LFBD", dto.OriginIcao);
        repoMock.Verify(r => r.AddAsync(It.IsAny<FlightPlan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_WrongIcao_IsInvalid() {
        var fetcherMock = new Mock<IOfpFetcher>();
        fetcherMock.Setup(f => f.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new MemoryStream());
        var readerMock = new Mock<ISimBriefOfpReader>();
        readerMock.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(ValidPlan());
        var repoMock = new Mock<IFlightPlanRepository>();
        var service = CreateService(fetcherMock, readerMock, repoMock);

        var dto = await service.DownloadAsync("A319", "12345", CancellationToken.None);

        Assert.Equal(ValidationState.Invalid, dto.ValidationState);
        Assert.False(dto.IsUsable);
    }
}
