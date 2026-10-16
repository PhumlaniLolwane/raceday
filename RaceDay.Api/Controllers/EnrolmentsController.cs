using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
public class EnrolmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    public EnrolmentsController(AppDbContext db) => _db = db;

    /// <summary>Enrol the logged-in Participant into a category. Records the link between Participant, event and category.</summary>
    [HttpPost("/api/enrolments")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(EnrolRequest request)
    {
        var userId = User.GetUserId();
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryID == request.CategoryId);
        if (category is null) return NotFound(new { message = "Category not found." });

        var existing = await _db.Enrolments
            .FirstOrDefaultAsync(e => e.ParticipantID == userId && e.CategoryID == category.CategoryID);
        if (existing is not null && existing.Status != "Cancelled")
            return Conflict(new { message = "You are already enrolled in this category." });

        var active = await _db.Enrolments
            .CountAsync(e => e.CategoryID == category.CategoryID && e.Status != "Cancelled");
        if (active >= category.MaxParticipants)
            return Conflict(new { message = "This category is full." });

        var today = DateOnly.FromDateTime(DateTime.Today);
        Enrolment enrolment;
        if (existing is not null)   // re-enrolling after a cancellation
        {
            existing.Status = "Registered";
            existing.EnrolmentDate = today;
            enrolment = existing;
        }
        else
        {
            enrolment = new Enrolment
            {
                ParticipantID = userId, CategoryID = category.CategoryID,
                EnrolmentDate = today, Status = "Registered"
            };
            _db.Enrolments.Add(enrolment);
        }
        await _db.SaveChangesAsync();

        var saved = await _db.Enrolments.WithDetails().FirstAsync(e => e.EnrolmentID == enrolment.EnrolmentID);
        return Created("/api/users/me/enrolments", EnrolmentDto.From(saved));
    }

    /// <summary>List the logged-in Participant's own enrolments and their status.</summary>
    [HttpGet("/api/users/me/enrolments")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        var userId = User.GetUserId();
        var list = await _db.Enrolments.WithDetails().Where(e => e.ParticipantID == userId).ToListAsync();
        return Ok(list.Select(EnrolmentDto.From).ToList());
    }

    /// <summary>List every enrolment for an event. Only the event's Organiser may do this.</summary>
    [HttpGet("/api/events/{eventId:int}/enrolments")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev is null) return NotFound();
        if (ev.OrganiserID != User.GetUserId()) return StatusCode(StatusCodes.Status403Forbidden);

        var list = await _db.Enrolments.WithDetails().Where(e => e.Category.EventID == eventId).ToListAsync();
        return Ok(list.Select(EnrolmentDto.From).ToList());
    }

    /// <summary>Cancel the logged-in Participant's own enrolment. The record is kept with status Cancelled.</summary>
    [HttpDelete("/api/enrolments/{id:int}")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int id)
    {
        var enrolment = await _db.Enrolments.FindAsync(id);
        if (enrolment is null) return NotFound();
        if (enrolment.ParticipantID != User.GetUserId()) return StatusCode(StatusCodes.Status403Forbidden);
        if (enrolment.Status == "Completed")
            return Conflict(new { message = "A completed enrolment cannot be cancelled." });

        enrolment.Status = "Cancelled";
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
