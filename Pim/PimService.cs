using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PIMTray.Pim;

public sealed class PimService
{
    private const string GraphBase = "https://graph.microsoft.com/v1.0";
    private readonly HttpClient _http;

    public PimService(HttpClient http)
    {
        _http = http;
    }

    public void SetAccessToken(string accessToken)
    {
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async Task<IReadOnlyList<EligibleRole>> GetEligibleRolesAsync(string userObjectId, CancellationToken ct = default)
    {
        var url = $"{GraphBase}/roleManagement/directory/roleEligibilitySchedules"
                + $"?$filter=principalId eq '{userObjectId}'"
                + "&$expand=roleDefinition";

        using var resp = await _http.GetAsync(url, ct);
        await EnsureSuccessAsync(resp, ct);

        var payload = await resp.Content.ReadFromJsonAsync<EligibilityResponse>(cancellationToken: ct)
                       ?? new EligibilityResponse();

        var list = new List<EligibleRole>();
        foreach (var s in payload.Value)
        {
            var name = s.RoleDefinition?.DisplayName ?? s.RoleDefinitionId ?? "(unknown role)";
            var scope = DescribeScope(s.DirectoryScopeId);
            list.Add(new EligibleRole(
                RoleDefinitionId: s.RoleDefinitionId ?? "",
                RoleDisplayName: name,
                DirectoryScopeId: s.DirectoryScopeId ?? "/",
                ScopeDescription: scope,
                MaxDurationHours: 8));
        }

        return list
            .OrderBy(r => r.RoleDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.ScopeDescription, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task ActivateRoleAsync(
        string userObjectId,
        EligibleRole role,
        string justification,
        TimeSpan duration,
        CancellationToken ct = default)
    {
        var body = new ActivateRequest
        {
            Action = "selfActivate",
            PrincipalId = userObjectId,
            RoleDefinitionId = role.RoleDefinitionId,
            DirectoryScopeId = role.DirectoryScopeId,
            Justification = justification,
            ScheduleInfo = new ScheduleInfo
            {
                StartDateTime = DateTimeOffset.UtcNow,
                Expiration = new ExpirationInfo
                {
                    Type = "AfterDuration",
                    Duration = $"PT{(int)duration.TotalHours}H"
                }
            }
        };

        using var resp = await _http.PostAsJsonAsync(
            $"{GraphBase}/roleManagement/directory/roleAssignmentScheduleRequests",
            body,
            new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull },
            ct);

        await EnsureSuccessAsync(resp, ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = await resp.Content.ReadAsStringAsync(ct);
        throw new PimApiException((int)resp.StatusCode, resp.ReasonPhrase ?? "", body);
    }

    private static string DescribeScope(string? scopeId) => scopeId switch
    {
        null or "" or "/" => "Directory",
        _ => scopeId
    };

    private sealed class EligibilityResponse
    {
        [JsonPropertyName("value")] public List<EligibilitySchedule> Value { get; set; } = new();
    }

    private sealed class EligibilitySchedule
    {
        [JsonPropertyName("roleDefinitionId")] public string? RoleDefinitionId { get; set; }
        [JsonPropertyName("directoryScopeId")] public string? DirectoryScopeId { get; set; }
        [JsonPropertyName("roleDefinition")] public RoleDefinition? RoleDefinition { get; set; }
    }

    private sealed class RoleDefinition
    {
        [JsonPropertyName("displayName")] public string? DisplayName { get; set; }
    }

    private sealed class ActivateRequest
    {
        [JsonPropertyName("action")] public string Action { get; set; } = "";
        [JsonPropertyName("principalId")] public string PrincipalId { get; set; } = "";
        [JsonPropertyName("roleDefinitionId")] public string RoleDefinitionId { get; set; } = "";
        [JsonPropertyName("directoryScopeId")] public string DirectoryScopeId { get; set; } = "/";
        [JsonPropertyName("justification")] public string Justification { get; set; } = "";
        [JsonPropertyName("scheduleInfo")] public ScheduleInfo ScheduleInfo { get; set; } = new();
    }

    private sealed class ScheduleInfo
    {
        [JsonPropertyName("startDateTime")] public DateTimeOffset StartDateTime { get; set; }
        [JsonPropertyName("expiration")] public ExpirationInfo Expiration { get; set; } = new();
    }

    private sealed class ExpirationInfo
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "AfterDuration";
        [JsonPropertyName("duration")] public string Duration { get; set; } = "PT1H";
    }
}

public sealed class PimApiException : Exception
{
    public int StatusCode { get; }
    public string ResponseBody { get; }

    public PimApiException(int statusCode, string reason, string body)
        : base($"Graph PIM call failed: HTTP {statusCode} {reason}. Body: {Truncate(body, 800)}")
    {
        StatusCode = statusCode;
        ResponseBody = body;
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "...";
}
