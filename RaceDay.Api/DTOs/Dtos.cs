using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.DTOs;

// ---------- Auth / users ----------
public class RegisterRequest
{
    [Required, StringLength(100)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8), StringLength(100)] public string Password { get; set; } = string.Empty;
    [Required, RegularExpression("^(Organiser|Participant)$", ErrorMessage = "Role must be 'Organiser' or 'Participant'.")]
    public string Role { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required, StringLength(100)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = string.Empty;
}

public class UserDto
{
    public int UserID { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public static UserDto From(AppUser u) => new()
    {
        UserID = u.UserID, FullName = u.FullName, Email = u.Email, Role = u.Role, CreatedAt = u.CreatedAt
    };
}

// ---------- Events ----------
public class EventRequest
{
    [Required, StringLength(150)] public string EventName { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Required] public DateOnly? EventDate { get; set; }
    [Required, StringLength(150)] public string Location { get; set; } = string.Empty;
    [Range(0.01, 9999.99)] public decimal DistanceKM { get; set; }
    [Required, RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "EventType must be 'Run', 'Walk' or 'Cycle'.")]
    public string EventType { get; set; } = string.Empty;
}

public class EventDto
{
    public int EventID { get; set; }
    public int OrganiserID { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal DistanceKM { get; set; }
    public string EventType { get; set; } = string.Empty;
    public List<CategoryDto>? Categories { get; set; }

    public static EventDto From(Event e, bool includeCategories = false) => new()
    {
        EventID = e.EventID, OrganiserID = e.OrganiserID, EventName = e.EventName,
        Description = e.Description, EventDate = e.EventDate, Location = e.Location,
        DistanceKM = e.DistanceKM, EventType = e.EventType,
        Categories = includeCategories ? e.Categories.Select(CategoryDto.From).ToList() : null
    };
}

// ---------- Categories ----------
public class CategoryRequest
{
    [Required, StringLength(100)] public string CategoryName { get; set; } = string.Empty;
    [Range(0.01, 9999.99)] public decimal DistanceKM { get; set; }
    [Range(1, 1000000)] public int MaxParticipants { get; set; } = 100;
    [Range(0, 999999.99)] public decimal EntryFee { get; set; }
}

public class CategoryDto
{
    public int CategoryID { get; set; }
    public int EventID { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal DistanceKM { get; set; }
    public int MaxParticipants { get; set; }
    public decimal EntryFee { get; set; }

    public static CategoryDto From(Category c) => new()
    {
        CategoryID = c.CategoryID, EventID = c.EventID, CategoryName = c.CategoryName,
        DistanceKM = c.DistanceKM, MaxParticipants = c.MaxParticipants, EntryFee = c.EntryFee
    };
}

// ---------- Enrolments ----------
public class EnrolRequest
{
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }
}

public class EnrolmentDto
{
    public int EnrolmentID { get; set; }
    public int ParticipantID { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int CategoryID { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int EventID { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateOnly EnrolmentDate { get; set; }
    public string Status { get; set; } = string.Empty;

    public static EnrolmentDto From(Enrolment e) => new()
    {
        EnrolmentID = e.EnrolmentID, ParticipantID = e.ParticipantID, ParticipantName = e.Participant.FullName,
        CategoryID = e.CategoryID, CategoryName = e.Category.CategoryName,
        EventID = e.Category.EventID, EventName = e.Category.Event.EventName,
        EnrolmentDate = e.EnrolmentDate, Status = e.Status
    };
}

// ---------- Results ----------
public class ResultRequest
{
    public TimeSpan? FinishTime { get; set; }
    [Range(1, 1000000)] public int? Position { get; set; }
    [Required, RegularExpression("^(Pending|Finished|DNF|DSQ)$", ErrorMessage = "Status must be Pending, Finished, DNF or DSQ.")]
    public string Status { get; set; } = string.Empty;
}

public class ResultDto
{
    public int ResultID { get; set; }
    public int EnrolmentID { get; set; }
    public int ParticipantID { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int EventID { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public TimeSpan? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = string.Empty;

    public static ResultDto From(Result r) => new()
    {
        ResultID = r.ResultID, EnrolmentID = r.EnrolmentID,
        ParticipantID = r.Enrolment.ParticipantID, ParticipantName = r.Enrolment.Participant.FullName,
        EventID = r.Enrolment.Category.EventID, EventName = r.Enrolment.Category.Event.EventName,
        CategoryName = r.Enrolment.Category.CategoryName,
        FinishTime = r.FinishTime, Position = r.Position, Status = r.Status
    };
}

/// <summary>Eager-loading helpers so the DTO mappers above always have what they need.</summary>
public static class QueryExtensions
{
    public static IQueryable<Enrolment> WithDetails(this IQueryable<Enrolment> q) =>
        q.Include(e => e.Participant).Include(e => e.Category).ThenInclude(c => c.Event);

    public static IQueryable<Result> WithDetails(this IQueryable<Result> q) =>
        q.Include(r => r.Enrolment).ThenInclude(e => e.Participant)
         .Include(r => r.Enrolment).ThenInclude(e => e.Category).ThenInclude(c => c.Event);
}
