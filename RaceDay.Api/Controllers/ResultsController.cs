using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
public class ResultsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ResultsController(AppDbContext db) => _db = db;

    /// <summary>Capture a finish time and position for an enrolment. Only the event's Organiser may do this.</summary>
    [HttpPost("/api/enrolments/{enrolmentId:int}/results")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(int enrolmentId, ResultRequest request)
    {
        var enrolment = await _db.Enrolments.WithDetails().FirstOrDefaultAsync(e => e.EnrolmentID == enrolmentId);
        if (enrolment is null) return NotFound();
        if (enrolment.Category.Event.OrganiserID != User.GetUserId()) return StatusCode(StatusCodes.Status403Forbidden);
        if (enrolment.Status == "Cancelled") return Conflict(new { message = "This enrolment was cancelled." });
        if (await _db.Results.AnyAsync(r => r.EnrolmentID == enrolmentId))
            return Conflict(new { message = "A result already exists for this enrolment. Use PUT /api/results/{id}." });
        if (request.Status == "Finished" && request.FinishTime is null)
            return BadRequest(new { message = "A finish time is required when the status is Finished." });

        var result = new Result
        {
            EnrolmentID = enrolmentId, FinishTime = request.FinishTime,
            Position = request.Position, Status = request.Status
        };
        if (request.Status == "Finished") enrolment.Status = "Completed";
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var saved = await _db.Results.WithDetails().FirstAsync(r => r.ResultID == result.ResultID);
        return Created($"/api/events/{saved.Enrolment.Category.EventID}/results", ResultDto.From(saved));
    }

    /// <summary>Correct a previously captured result. Only the event's Organiser may do this.</summary>
    [HttpPut("/api/results/{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, ResultRequest request)
    {
        var result = await _db.Results.WithDetails().FirstOrDefaultAsync(r => r.ResultID == id);
        if (result is null) return NotFound();
        if (result.Enrolment.Category.Event.OrganiserID != User.GetUserId()) return StatusCode(StatusCodes.Status403Forbidden);
        if (request.Status == "Finished" && request.FinishTime is null)
            return BadRequest(new { message = "A finish time is required when the status is Finished." });

        result.FinishTime = request.FinishTime;
        result.Position = request.Position;
        result.Status = request.Status;
        result.Enrolment.Status = request.Status == "Finished" ? "Completed" : "Registered";
        await _db.SaveChangesAsync();
        return Ok(ResultDto.From(result));
    }

    /// <summary>The logged-in Participant's own results across all events.</summary>
    [HttpGet("/api/users/me/results")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(List<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        var userId = User.GetUserId();
        var list = await _db.Results.WithDetails().Where(r => r.Enrolment.ParticipantID == userId).ToListAsync();
        return Ok(list.Select(ResultDto.From).ToList());
    }

    /// <summary>The results list (leaderboard) for an event, best position first (public).</summary>
    [HttpGet("/api/events/{eventId:int}/results")]
    [ProducesResponseType(typeof(List<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventID == eventId)) return NotFound();
        var list = await _db.Results.WithDetails().Where(r => r.Enrolment.Category.EventID == eventId).ToListAsync();
        return Ok(list.OrderBy(r => r.Position ?? int.MaxValue)
                      .ThenBy(r => r.FinishTime ?? TimeSpan.MaxValue)
                      .Select(ResultDto.From).ToList());
    }
}
