public class TelegramService
{
    private readonly ILogger<TelegramService> _logger;
    private readonly string _botToken;
    private readonly string _chatId;
    private readonly int _cooldownMinutes;
    private readonly HttpClient _http = new();

    // Thresholds — loaded from appsettings, updatable at runtime via API
    public double MinTemp     { get; set; }
    public double MaxTemp     { get; set; }
    public double MinHumidity { get; set; }
    public double MaxHumidity { get; set; }

    // Cooldown tracking per alert type
    private readonly Dictionary<string, DateTime> _lastSent = new();
    private readonly object _lock = new();

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_botToken) && !_botToken.StartsWith("YOUR_") &&
        !string.IsNullOrWhiteSpace(_chatId)   && !_chatId.StartsWith("YOUR_");

    public TelegramService(IConfiguration config, ILogger<TelegramService> logger)
    {
        _logger         = logger;
        _botToken       = config["Telegram:BotToken"]  ?? "";
        _chatId         = config["Telegram:ChatId"]    ?? "";
        _cooldownMinutes = config.GetValue<int>("Telegram:AlertCooldownMinutes", 10);

        MinTemp      = config.GetValue<double>("AlertThresholds:MinTemp",      2.0);
        MaxTemp      = config.GetValue<double>("AlertThresholds:MaxTemp",      8.0);
        MinHumidity  = config.GetValue<double>("AlertThresholds:MinHumidity", 60.0);
        MaxHumidity  = config.GetValue<double>("AlertThresholds:MaxHumidity", 85.0);
    }

    public async Task CheckAndAlertAsync(double temperature, double humidity, string deviceId)
    {
        if (!IsConfigured) return;

        // Build list of breached conditions
        var alerts = new List<(string key, string message)>();

        if (temperature < MinTemp)
            alerts.Add(("TEMP_LOW",
                $"🥶 *Nhiệt độ quá thấp!*\nGiá trị: `{temperature:F1}°C` — Ngưỡng min: `{MinTemp}°C`"));

        else if (temperature > MaxTemp)
            alerts.Add(("TEMP_HIGH",
                $"🔥 *Nhiệt độ quá cao!*\nGiá trị: `{temperature:F1}°C` — Ngưỡng max: `{MaxTemp}°C`"));

        if (humidity < MinHumidity)
            alerts.Add(("HUM_LOW",
                $"🏜️ *Độ ẩm quá thấp!*\nGiá trị: `{humidity:F1}%` — Ngưỡng min: `{MinHumidity}%`"));

        else if (humidity > MaxHumidity)
            alerts.Add(("HUM_HIGH",
                $"💧 *Độ ẩm quá cao!*\nGiá trị: `{humidity:F1}%` — Ngưỡng max: `{MaxHumidity}%`"));

        foreach (var (key, detail) in alerts)
        {
            if (!CanSend(key)) continue;

            var text = $"⚠️ *CẢNH BÁO KHO LẠNH*\n\n" +
                       $"{detail}\n\n" +
                       $"📟 Thiết bị: `{deviceId}`\n" +
                       $"🕐 Thời gian: `{DateTime.Now:dd/MM/yyyy HH:mm:ss}`";

            await SendAsync(text);
            MarkSent(key);
        }
    }

    public async Task SendTestAsync() =>
        await SendAsync(
            $"✅ *Test kết nối thành công!*\n\n" +
            $"Bot đang hoạt động bình thường.\n" +
            $"🕐 `{DateTime.Now:dd/MM/yyyy HH:mm:ss}`");

    private bool CanSend(string key)
    {
        lock (_lock)
        {
            if (!_lastSent.TryGetValue(key, out var last)) return true;
            return (DateTime.UtcNow - last).TotalMinutes >= _cooldownMinutes;
        }
    }

    private void MarkSent(string key)
    {
        lock (_lock) { _lastSent[key] = DateTime.UtcNow; }
    }

    private async Task SendAsync(string text)
    {
        try
        {
            var url     = $"https://api.telegram.org/bot{_botToken}/sendMessage";
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                chat_id    = _chatId,
                text       = text,
                parse_mode = "Markdown"
            });
            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            var res     = await _http.PostAsync(url, content);

            if (res.IsSuccessStatusCode)
                _logger.LogInformation("Telegram alert sent OK");
            else
            {
                var body = await res.Content.ReadAsStringAsync();
                _logger.LogWarning("Telegram error {Status}: {Body}", res.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram SendAsync failed");
        }
    }
}
