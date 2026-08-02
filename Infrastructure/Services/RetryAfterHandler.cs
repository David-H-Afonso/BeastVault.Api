namespace BeastVault.Api.Infrastructure.Services;

/// Retries provider throttling without retrying arbitrary failed requests.
public sealed class RetryAfterHandler : DelegatingHandler
{
    private const int MaxRetries = 2;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode != System.Net.HttpStatusCode.TooManyRequests || attempt >= MaxRetries)
                return response;

            var delay = response.Headers.RetryAfter?.Delta
                ?? (response.Headers.RetryAfter?.Date is { } retryAt
                    ? retryAt - DateTimeOffset.UtcNow
                    : TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)));
            response.Dispose();
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(delay.TotalSeconds, 1, 30)), cancellationToken);
        }
    }
}
