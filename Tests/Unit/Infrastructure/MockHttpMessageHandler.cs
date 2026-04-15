using System.Net;
using System.Text;

namespace Tests.Unit.Infrastructure;

/// <summary>
/// Handler HTTP fittizio per test unitari.
/// Permette di configurare risposte predefinite per URL specifici.
/// Crea una nuova HttpResponseMessage per ogni chiamata (evita ObjectDisposedException).
/// </summary>
internal class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (string Json, HttpStatusCode Status)> _responses =
        new(StringComparer.OrdinalIgnoreCase);

    private HttpStatusCode _defaultStatus = HttpStatusCode.NotFound;

    /// <summary>Registra una risposta JSON per un URL (matching per Contains).</summary>
    public void SetJsonResponse(string urlContains, string json,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses[urlContains] = (json, status);
    }

    /// <summary>Registra la risposta di default per URL non matchati.</summary>
    public void SetDefaultResponse(HttpStatusCode status)
    {
        _defaultStatus = status;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var url = request.RequestUri?.ToString() ?? string.Empty;

        // Matcha le chiavi più lunghe prima (più specifiche)
        foreach (var (key, (json, status)) in _responses
            .OrderByDescending(r => r.Key.Length))
        {
            if (url.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(json, Encoding.UTF8,
                        "application/json")
                });
            }
        }

        return Task.FromResult(new HttpResponseMessage(_defaultStatus)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });
    }
}
