namespace RaceDay.Api.Models;

public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

/// <summary>Maps to the Users table.</summary>
public class AppUser
{
    public int UserID { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Participant;
    public DateTime CreatedAt { get; set; }

    public List<Event> Events { get; set; } = new();
    public List<Enrolment> Enrolments { get; set; } = new();
}
