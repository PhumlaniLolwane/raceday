namespace RaceDay.Api.Models;

/// <summary>Maps to the Routes table. Named RaceRoute to avoid clashing with ASP.NET's [Route].</summary>
public class RaceRoute
{
    public int RouteID { get; set; }
    public int CategoryID { get; set; }
    public string StartPoint { get; set; } = string.Empty;
    public string EndPoint { get; set; } = string.Empty;
    public string? MapURL { get; set; }

    public Category Category { get; set; } = null!;
}
