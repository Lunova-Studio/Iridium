namespace Iridium.Models.Resources;


public sealed record ResourceUser {
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? AvatarUrl { get; init; }
    public string? ProfileUrl { get; init; }
}
