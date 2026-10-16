using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly AppDbContext _db;
    public EventsController(AppDbContext db) => _db = db;

    /// <summary>List upcoming events (public). Optional filters: location, eventType, fromDate, toDate. Set includePast=true to include past events.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? location, [FromQuery] string? eventType,
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
        [FromQuery] bool includePast = false)
    {
        var query = _db.Events.AsQueryable();
        if (!includePast && fromDate is null)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            query = query.Where(e => e.EventDate >= today);
        }
        if (fromDate is not null) query = query.Where(e => e.EventDate >= fromDate);
        if (toDate is not null) query = query.Where(e => e.EventDate <= toDate);
        if (!string.IsNullOrWhiteSpace(location)) query = query.Where(e => e.Location.Contains(location));
        if (!string.IsNullOrWhiteSpace(eventType)) query = query.Where(e => e.EventType == eventType);

        var events = await query.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(e => EventDto.From(e)).ToList());
    }

    /// <summary>Get one event with its categories (public).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var ev = await _db.Events.Include(e => e.Categories).FirstOrDefaultAsync(e => e.EventID == id);
        return ev is null ? NotFound() : Ok(EventDto.From(ev, includeCategories: true));
    }

    /// <summary>Create an event owned by the logged-in Organiser.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(EventRequest request)
    {
        var ev = new Event
        {
            OrganiserID = User.GetUserId(),
            EventName = request.EventName.Trim(),
            Description = request.Description,
            EventDate = request.EventDate!.Value,
            Location = request.Location.Trim(),
            DistanceKM = request.DistanceKM,
            EventType = request.EventType,
            CreatedAt = DateTime.UtcNow
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = ev.EventID }, EventDto.From(ev));
    }

    /// <summary>Update an event. Only the Organiser who owns it may do this.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, EventRequest request)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev is null) return NotFound();
        if (ev.OrganiserID != User.GetUserId()) return StatusCode(StatusCodes.Status403Forbidden);

        ev.EventName = request.EventName.Trim();
        ev.Description = request.Description;
        ev.EventDate = request.EventDate!.Value;
        ev.Location = request.Location.Trim();
        ev.DistanceKM = request.DistanceKM;
        ev.EventType = request.EventType;
        await _db.SaveChangesAsync();
        return Ok(EventDto.From(ev));
    }

    /// <summary>Delete an event and its categories. Only the owning Organiser may do this; refused (409) if anyone has enrolled.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _db.Events.Include(e => e.Categories).FirstOrDefaultAsync(e => e.EventID == id);
        if (ev is null) return NotFound();
        if (ev.OrganiserID != User.GetUserId()) return StatusCode(StatusCodes.Status403Forbidden);

        var categoryIds = ev.Categories.Select(c => c.CategoryID).ToList();
        if (await _db.Enrolments.AnyAsync(en => categoryIds.Contains(en.CategoryID)))
            return Conflict(new { message = "This event has enrolments and cannot be deleted." });

        _db.Routes.RemoveRange(await _db.Routes.Where(r => categoryIds.Contains(r.CategoryID)).ToListAsync());
        _db.Categories.RemoveRange(ev.Categories);
        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
