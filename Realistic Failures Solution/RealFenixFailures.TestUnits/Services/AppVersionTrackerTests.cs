using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Services;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Interfaces.Repositories;

namespace RealFenixFailures.TestUnits.Services;

public class AppVersionTrackerTests {
    private sealed class FakeVersionProvider : IAppVersionProvider {
        private readonly string _version;
        public FakeVersionProvider(string version) { _version = version; }
        public string CurrentVersion => _version;
    }

    private static AppVersionTracker CreateTracker(Mock<IAppVersionHistoryRepository> repository, string currentVersion, out FakeVersionProvider provider) {
        provider = new FakeVersionProvider(currentVersion);
        return new AppVersionTracker(repository.Object, provider, NullLogger<AppVersionTracker>.Instance);
    }

    private static Mock<IAppVersionHistoryRepository> CreateRepository(string? latestVersion) {
        var repository = new Mock<IAppVersionHistoryRepository>();
        repository.Setup(r => r.GetLatestVersionAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(latestVersion);
        repository.Setup(r => r.AddAsync(It.IsAny<AppVersionHistory>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);
        return repository;
    }

    [Fact]
    public async Task InitializeAsync_PrimeraEjecucion_RegistraVersionActual() {
        const string currentVersion = "0.4.1-beta.1";
        var repository = CreateRepository(null);
        var tracker = CreateTracker(repository, currentVersion, out _);

        await tracker.InitializeAsync(CancellationToken.None);

        repository.Verify(r => r.GetLatestVersionAsync(It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.AddAsync(It.Is<AppVersionHistory>(e => e.AppVersion == currentVersion), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_MismaVersion_NoRegistraNada() {
        const string currentVersion = "0.4.1-beta.1";
        var repository = CreateRepository(currentVersion);
        var tracker = CreateTracker(repository, currentVersion, out _);

        await tracker.InitializeAsync(CancellationToken.None);

        repository.Verify(r => r.GetLatestVersionAsync(It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.AddAsync(It.IsAny<AppVersionHistory>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InitializeAsync_NuevaVersion_RegistraTransicion() {
        const string currentVersion = "0.4.1-beta.1";
        var repository = CreateRepository("0.4.0-beta.1");
        var tracker = CreateTracker(repository, currentVersion, out _);

        await tracker.InitializeAsync(CancellationToken.None);

        repository.Verify(r => r.GetLatestVersionAsync(It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.AddAsync(It.Is<AppVersionHistory>(e => e.AppVersion == currentVersion), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_Downgrade_TambienRegistra() {
        const string currentVersion = "0.4.1-beta.1";
        var repository = CreateRepository("0.5.0-beta.1");
        var tracker = CreateTracker(repository, currentVersion, out _);

        await tracker.InitializeAsync(CancellationToken.None);

        repository.Verify(r => r.GetLatestVersionAsync(It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.AddAsync(It.Is<AppVersionHistory>(e => e.AppVersion == currentVersion), It.IsAny<CancellationToken>()), Times.Once);
    }
}
