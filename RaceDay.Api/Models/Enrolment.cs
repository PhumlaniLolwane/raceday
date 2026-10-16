namespace RaceDay.Api.Models;

public class Enrolment
{
    public int EnrolmentID { get; set; }
    public int ParticipantID { get; set; }
    public int CategoryID { get; set; }
    public DateOnly EnrolmentDate { get; set; }
    public string Status { get; set; } = "Registered";  // Registered, Cancelled, Completed

    public AppUser Participant { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Result? Result { get; set; }
}
