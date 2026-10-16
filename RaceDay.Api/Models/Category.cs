namespace RaceDay.Api.Models;

public class Category
{
    public int CategoryID { get; set; }
    public int EventID { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal DistanceKM { get; set; }
    public int MaxParticipants { get; set; } = 100;
    public decimal EntryFee { get; set; }

    public Event Event { get; set; } = null!;
    public RaceRoute? Route { get; set; }
    public List<Enrolment> Enrolments { get; set; } = new();
}
