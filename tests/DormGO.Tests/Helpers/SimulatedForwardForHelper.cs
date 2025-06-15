namespace DormGO.Tests.Helpers;

public class SimulatedForwardedForHandler : DelegatingHandler
{
    private readonly string _simulatedIp;

    public SimulatedForwardedForHandler(HttpMessageHandler innerHandler, string simulatedIp)
        : base(innerHandler)
    {
        _simulatedIp = simulatedIp;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Contains("X-Forwarded-For"))
        {
            request.Headers.Remove("X-Forwarded-For");
        }
        request.Headers.Add("X-Forwarded-For", _simulatedIp);

        return base.SendAsync(request, cancellationToken);
    }
}