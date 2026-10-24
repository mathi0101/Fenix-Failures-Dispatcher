namespace RealFenixFailures.Integrations.SimBrief.Interfaces;

public interface ISimBriefApiClient {
    Task<Stream> FetchOfpXmlAsync(string simbriefUserId, CancellationToken ct);
}
