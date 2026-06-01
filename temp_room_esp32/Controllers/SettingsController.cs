using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly TelegramService _telegram;

    public SettingsController(TelegramService telegram) => _telegram = telegram;

    // GET /api/settings/thresholds — trả về ngưỡng hiện tại
    [HttpGet("thresholds")]
    public IActionResult GetThresholds() => Ok(new
    {
        minTemp     = _telegram.MinTemp,
        maxTemp     = _telegram.MaxTemp,
        minHumidity = _telegram.MinHumidity,
        maxHumidity = _telegram.MaxHumidity
    });

    // POST /api/settings/thresholds — cập nhật ngưỡng trong bộ nhớ
    [HttpPost("thresholds")]
    public IActionResult SetThresholds([FromBody] ThresholdDto dto)
    {
        if (dto.MinTemp >= dto.MaxTemp)
            return BadRequest(new { error = "Nhiệt độ thấp nhất phải nhỏ hơn nhiệt độ cao nhất." });
        if (dto.MinHumidity >= dto.MaxHumidity)
            return BadRequest(new { error = "Độ ẩm thấp nhất phải nhỏ hơn độ ẩm cao nhất." });

        _telegram.MinTemp     = dto.MinTemp;
        _telegram.MaxTemp     = dto.MaxTemp;
        _telegram.MinHumidity = dto.MinHumidity;
        _telegram.MaxHumidity = dto.MaxHumidity;

        return Ok(new { message = "Đã cập nhật ngưỡng cảnh báo." });
    }

    // POST /api/settings/telegram-test — gửi tin nhắn test
    [HttpPost("telegram-test")]
    public async Task<IActionResult> TelegramTest()
    {
        if (!_telegram.IsConfigured)
            return BadRequest(new { error = "Telegram chưa được cấu hình. Kiểm tra BotToken và ChatId trong appsettings.json." });

        await _telegram.SendTestAsync();
        return Ok(new { message = "Đã gửi tin nhắn test tới Telegram." });
    }

    // GET /api/settings/telegram-status
    [HttpGet("telegram-status")]
    public IActionResult TelegramStatus() => Ok(new { configured = _telegram.IsConfigured });

    public record ThresholdDto(double MinTemp, double MaxTemp, double MinHumidity, double MaxHumidity);
}
