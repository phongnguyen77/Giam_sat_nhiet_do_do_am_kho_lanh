using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

public class MqttDataService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<MqttHub> _hubContext;
    private readonly ILogger<MqttDataService> _logger;
    private readonly TelegramService _telegram;

    public MqttDataService(IServiceScopeFactory scopeFactory, IHubContext<MqttHub> hubContext,
        ILogger<MqttDataService> logger, TelegramService telegram)
    {
        _scopeFactory = scopeFactory;
        _hubContext   = hubContext;
        _logger       = logger;
        _telegram     = telegram;
    }

    private object? _latestData;
    public object? LatestData => _latestData;

    private IMqttClient? _client;

    // history storage
    private readonly List<SensorReading> _history = new();
    private readonly object _historyLock = new();
    private const int MaxHistory = 500;

    public IReadOnlyList<SensorReading> GetHistory()
    {
        lock (_historyLock)
        {
            return _history.ToArray();
        }
    }

    public async Task StartAsync()
    {
        _logger.LogInformation("Starting MQTT service...");
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("da9cd5e6dd2340789dd109e41d1b6e10.s1.eu.hivemq.cloud", 8883)
            .WithCredentials("phongtech", "Phong2025")
            .WithTls()
            .Build();

        _client.ConnectedAsync += async e =>
        {
            _logger.LogInformation("MQTT connected. Subscribing to topic...");
            try
            {
                await _client.SubscribeAsync("device/sensor/temp_007", MqttQualityOfServiceLevel.AtMostOnce);
                _logger.LogInformation("Subscribed to topic device/sensor/temp_007");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to topic");
            }
        };

        _client.ApplicationMessageReceivedAsync += async e =>
        {
            try
            {
                var topic = e.ApplicationMessage.Topic;
                var payload = System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.Payload ?? System.Array.Empty<byte>());
                _logger.LogInformation("MQTT message received on topic {topic}: {payload}", topic, payload);

                // try to parse the JSON and extract temperature/humidity/timestamp/device id
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;

                double? temperature = null;
                double? humidity = null;
                DateTime timestamp = DateTime.UtcNow;
                string deviceId = string.Empty;

                if (root.TryGetProperty("device", out var deviceEl) && deviceEl.ValueKind == JsonValueKind.Object)
                {
                    if (deviceEl.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                        deviceId = idEl.GetString() ?? string.Empty;
                }
                else if (root.TryGetProperty("deviceId", out var devId2) && devId2.ValueKind == JsonValueKind.String)
                {
                    deviceId = devId2.GetString() ?? string.Empty;
                }

                if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
                {
                    if (dataEl.TryGetProperty("temperature", out var tEl) && (tEl.ValueKind == JsonValueKind.Number || tEl.ValueKind == JsonValueKind.String))
                    {
                        if (tEl.ValueKind == JsonValueKind.Number && tEl.TryGetDouble(out var d)) temperature = d;
                        else if (tEl.ValueKind == JsonValueKind.String && double.TryParse(tEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var ds)) temperature = ds;
                    }
                    if (dataEl.TryGetProperty("humidity", out var hEl) && (hEl.ValueKind == JsonValueKind.Number || hEl.ValueKind == JsonValueKind.String))
                    {
                        if (hEl.ValueKind == JsonValueKind.Number && hEl.TryGetDouble(out var d2)) humidity = d2;
                        else if (hEl.ValueKind == JsonValueKind.String && double.TryParse(hEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var ds2)) humidity = ds2;
                    }
                }

                if (temperature == null)
                {
                    if (root.TryGetProperty("temperature", out var t2) && t2.TryGetDouble(out var d3)) temperature = d3;
                }
                if (humidity == null)
                {
                    if (root.TryGetProperty("humidity", out var h2) && h2.TryGetDouble(out var d4)) humidity = d4;
                }

                if (root.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String)
                {
                    var s = ts.GetString();
                    if (!string.IsNullOrEmpty(s) && DateTimeOffset.TryParse(s, out var dto)) timestamp = dto.UtcDateTime;
                }
                else if (root.TryGetProperty("data", out var droot) && droot.ValueKind == JsonValueKind.Object && droot.TryGetProperty("timestamp", out var ts2) && ts2.ValueKind == JsonValueKind.String)
                {
                    var s2 = ts2.GetString();
                    if (!string.IsNullOrEmpty(s2) && DateTimeOffset.TryParse(s2, out var dto2)) timestamp = dto2.UtcDateTime;
                }

                if (temperature.HasValue && humidity.HasValue)
                {
                    var reading = new SensorReading(timestamp, temperature.Value, humidity.Value, deviceId);

                    lock (_historyLock)
                    {
                        _history.Add(reading);
                        if (_history.Count > MaxHistory)
                        {
                            _history.RemoveRange(0, _history.Count - MaxHistory);
                        }
                    }

                    // save to sqlite
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var record = new TemperatureRecord { Timestamp = timestamp, Temperature = temperature.Value, Humidity = humidity.Value, DeviceId = deviceId };
                        db.TemperatureRecords.Add(record);
                        await db.SaveChangesAsync();
                        _logger.LogInformation("Saved record to DB: {id} {ts} {temp}", record.Id, record.Timestamp, record.Temperature);

                        // push to SignalR clients as soon as saved
                        await _hubContext.Clients.All.SendAsync("ReceiveReading", new {
                            timestamp = record.Timestamp,
                            temperature = record.Temperature,
                            humidity = record.Humidity,
                            deviceId = record.DeviceId
                        });
                        _logger.LogInformation("Pushed reading to SignalR clients");
                    }
                    catch (System.Exception ex)
                    {
                        _logger.LogError(ex, "Error saving to DB or pushing to SignalR");
                    }

                    // kiểm tra ngưỡng & gửi Telegram nếu cần (fire-and-forget)
                    _ = _telegram.CheckAndAlertAsync(reading.Temperature, reading.Humidity, reading.DeviceId);

                    _latestData = reading;
                }
                else
                {
                    _latestData = JsonSerializer.Deserialize<JsonElement>(payload);
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error processing MQTT message");
                _latestData = null;
            }
        };

        try
        {
            if (_client != null)
            {
                await _client.ConnectAsync(options, CancellationToken.None);
                _logger.LogInformation("MQTT ConnectAsync returned");
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "MQTT connect failed");
        }
    }
}

public record SensorReading(DateTime Timestamp, double Temperature, double Humidity, string DeviceId);
