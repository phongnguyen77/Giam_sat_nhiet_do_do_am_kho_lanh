using System;

public class TemperatureRecord
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public string DeviceId { get; set; } = string.Empty;
}
