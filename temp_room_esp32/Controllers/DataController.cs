using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    private readonly MqttDataService _mqttService;
    private readonly AppDbContext _db;

    public DataController(MqttDataService mqttService, AppDbContext db)
    {
        _mqttService = mqttService;
        _db = db;
    }

    [HttpGet]
    public IActionResult Get()
    {
        if (_mqttService.LatestData == null)
            return NotFound();
        return Ok(_mqttService.LatestData);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        // return last 200 records ordered descending by timestamp
        var items = await _db.TemperatureRecords
            .AsNoTracking()
            .OrderByDescending(r => r.Timestamp)
            .Take(200)
            .Select(r => new {
                timestamp = r.Timestamp,
                temperature = r.Temperature,
                humidity = r.Humidity,
                deviceId = r.DeviceId
            })
            .ToListAsync();

        return Ok(items);
    }
}
