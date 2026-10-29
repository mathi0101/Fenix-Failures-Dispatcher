using RealFenixFailures.Application.Interfaces;

namespace RealFenixFailures.UI.Services;

public class AppVersionProvider : IAppVersionProvider {
    public string CurrentVersion => AppVersionInfo.Current;
}
