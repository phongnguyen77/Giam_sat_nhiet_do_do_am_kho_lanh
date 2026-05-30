using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    private readonly MqttDataService  _mqttService;
    private readonly AppDbContext     _db;
    private readonly FirebaseService  _firebase;

    public DataController(MqttDataService mqttService, AppDbContext db, FirebaseService firebase)
    {
        _mqttService = mqttService;
        _db          = db;
        _firebase    = firebase;
    }

    [HttpGet]
    public IActionResult Get()
    {
        if (_mqttService.LatestData == null)
            return NotFound();
        return Ok(_mqttService.LatestData);
    }

    // Lịch sử từ SQLite (local)
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var items = await _db.TemperatureRecords
            .AsNoTracking()
            .OrderByDescending(r => r.Timestamp)
            .Take(200)
            .Select(r => new {
                timestamp   = r.Timestamp,
                temperature = r.Temperature,
                humidity    = r.Humidity,
                deviceId    = r.DeviceId
            })
            .ToListAsync();

        return Ok(items);
    }

    // Lịch sử từ Firebase Realtime Database
    // GET /api/data/firebase-history?from=2026-05-30T00:00:00&to=2026-05-30T23:59:59
    [HttpGet("firebase-history")]
    public async Task<IActionResult> GetFirebaseHistory(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        if (!_firebase.IsConfigured)
            return StatusCode(503, new { error = "Firebase chưa được cấu hình. Vui lòng điền DatabaseUrl và đặt file service account." });

        var fromDate = (from ?? DateTime.UtcNow.Date).ToUniversalTime();
        var toDate   = (to   ?? fromDate.AddDays(1).AddTicks(-1)).ToUniversalTime();

        if (fromDate > toDate)
            return BadRequest(new { error = "Ngày bắt đầu phải trước ngày kết thúc." });

        try
        {
            var items = await _firebase.GetHistoryAsync(fromDate, toDate);
            return Ok(items.Select(r => new {
                timestamp   = r.Timestamp,
                temperature = r.Temperature,
                humidity    = r.Humidity,
                deviceId    = r.DeviceId
            }));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Lỗi khi đọc Firebase: {ex.Message}" });
        }
    }
}
