namespace RaceDay.Api.Models;

public class Event
{
    public int EventID { get; set; }
    public int OrganiserID { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal DistanceKM { get; set; }
    public string EventType { get; set; } = "Run";   // Run, Walk or Cycle
    public DateTime CreatedAt { get; set; }

    public AppUser Organiser { get; set; } = null!;
    public List<Category> Categories { get; set; } = new();
}
