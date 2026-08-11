namespace Iridium.Models;

public record IridiumContext {
    public string UserAgent { get; set; } = "Iridium/1.0";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);
}