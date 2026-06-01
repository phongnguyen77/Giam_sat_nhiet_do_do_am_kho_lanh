using Google.Apis.Auth.OAuth2;
using System.Text.Json;

public class FirebaseService
{
    private readonly string _databaseUrl;
    private readonly string _credentialPath;
    private readonly string _sensorDataPath;
    private readonly string _deviceId;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<FirebaseService> _logger;

    private static readonly string[] FirebaseScopes =
    [
        "https://www.googleapis.com/auth/firebase.database",
        "https://www.googleapis.com/auth/userinfo.email"
    ];

    public FirebaseService(IConfiguration config, IHttpClientFactory httpFactory, ILogger<FirebaseService> logger)
    {
        _databaseUrl    = (config["Firebase:DatabaseUrl"] ?? "").TrimEnd('/');
        _credentialPath = config["Firebase:CredentialPath"] ?? "firebase-service-account.json";
        _sensorDataPath = config["Firebase:SensorDataPath"] ?? "sensor_data";
        _deviceId       = config["Firebase:DeviceId"]       ?? "temp_007";
        _httpFactory    = httpFactory;
        _logger         = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_databaseUrl) &&
        !_databaseUrl.Contains("YOUR-PROJECT") &&
        File.Exists(_credentialPath);

    private async Task<string> GetAccessTokenAsync()
    {
        var credential = GoogleCredential
            .FromFile(_credentialPath)
            .CreateScoped(FirebaseScopes);
        return await credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
    }

    /// <summary>
    /// Đọc lịch sử từ sensor_data/{deviceId} theo khoảng epochMs.
    /// Key trong RTDB là epochMs (số nguyên), nên dùng orderBy="$key" + startAt/endAt là epochMs.
    /// </summary>
    public async Task<List<SensorReading>> GetHistoryAsync(DateTime from, DateTime to)
    {
        var token       = await GetAccessTokenAsync();
        var fromEpochMs = new DateTimeOffset(from.ToUniversalTime()).ToUnixTimeMilliseconds();
        var toEpochMs   = new DateTimeOffset(to.ToUniversalTime()).ToUnixTimeMilliseconds();
        var path        = $"{_sensorDataPath}/{_deviceId}";

        // RTDB: key là epochMs (string) → orderBy="$key", startAt/endAt cũng là string có dấu nháy
        var orderBy   = Uri.EscapeDataString("\"$key\"");
        var startAtStr = Uri.EscapeDataString($"\"{fromEpochMs}\"");
        var endAtStr   = Uri.EscapeDataString($"\"{toEpochMs}\"");

        var url = $"{_databaseUrl}/{path}.json" +
                  $"?access_token={Uri.EscapeDataString(token)}" +
                  $"&orderBy={orderBy}" +
                  $"&startAt={startAtStr}" +
                  $"&endAt={endAtStr}";

        _logger.LogInformation("Firebase query: {path} | {from} → {to} ({fromMs}→{toMs})",
            path, from, to, fromEpochMs, toEpochMs);

        var client = _httpFactory.CreateClient("firebase");
        var json   = await client.GetStringAsync(url);

        if (json == "null" || string.IsNullOrWhiteSpace(json))
            return [];

        var result = new List<SensorReading>();

        using var doc = JsonDocument.Parse(json);

        foreach (var node in doc.RootElement.EnumerateObject())
        {
            var el = node.Value;
            if (el.ValueKind != JsonValueKind.Object) continue;

            double   temp     = 0;
            double   hum      = 0;
            DateTime ts       = DateTime.UtcNow;
            string   deviceId = _deviceId;

            if (el.TryGetProperty("temperature", out var tEl))
            {
                if (tEl.ValueKind == JsonValueKind.Number) tEl.TryGetDouble(out temp);
                else if (tEl.ValueKind == JsonValueKind.String)
                    double.TryParse(tEl.GetString(), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out temp);
            }

            if (el.TryGetProperty("humidity", out var hEl))
            {
                if (hEl.ValueKind == JsonValueKind.Number) hEl.TryGetDouble(out hum);
                else if (hEl.ValueKind == JsonValueKind.String)
                    double.TryParse(hEl.GetString(), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out hum);
            }

            // timestamp field từ backend: "yyyy-MM-ddTHH:mm:ssZ"
            if (el.TryGetProperty("timestamp", out var tsEl) && tsEl.ValueKind == JsonValueKind.String)
            {
                if (DateTimeOffset.TryParse(tsEl.GetString(), out var dto))
                    ts = dto.UtcDateTime;
            }
            else
            {
                // fallback: dùng key (epochMs) làm timestamp
                if (long.TryParse(node.Name, out var epochMs))
                    ts = DateTimeOffset.FromUnixTimeMilliseconds(epochMs).UtcDateTime;
            }

            if (el.TryGetProperty("deviceId", out var dEl) && dEl.ValueKind == JsonValueKind.String)
                deviceId = dEl.GetString() ?? _deviceId;

            result.Add(new SensorReading(ts, temp, hum, deviceId));
        }

        return [.. result.OrderByDescending(r => r.Timestamp)];
    }
}
