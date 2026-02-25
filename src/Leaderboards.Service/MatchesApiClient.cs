using System.Net.Http.Json;
using Leaderboards.Contracts;

namespace Leaderboards.Service;

public class MatchesApiClient(HttpClient httpClient)
{
    public async Task<List<MatchResponse>> GetMatchesForVenueAsync(string venueName, CancellationToken ct = default)
    {
        return await httpClient.GetFromJsonAsync<List<MatchResponse>>(
            $"/matches?venueName={Uri.EscapeDataString(venueName)}", ct) ?? [];
    }
}
