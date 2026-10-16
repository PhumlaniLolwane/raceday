using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    public CategoriesController(AppDbContext db) => _db = db;

    /// <summary>List the categories (e.g. Under 20, Senior, 10km, 21km) of an event (public).</summary>
    [HttpGet("/api/events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventID == eventId)) return NotFound();
        var categories = await _db.Categories.Where(c => c.EventID == eventId).OrderBy(c => c.DistanceKM).ToListAsync();
        return Ok(categories.Select(CategoryDto.From).ToList());
    }

    /// <summary>Add a category to an event. Only the event's Organiser may do this.</summary>
    [HttpPost("/api/events/{eventId:int}/categories")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(int eventId, CategoryRequest request)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev is null) return NotFound();
        if (ev.OrganiserID != User.GetUserId()) return StatusCode(StatusCodes.Status403Forbidden);

        var category = new Category
        {
            EventID = eventId,
            CategoryName = request.CategoryName.Trim(),
            DistanceKM = request.DistanceKM,
            MaxParticipants = request.MaxParticipants,
            EntryFee = request.EntryFee
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        return Created($"/api/events/{eventId}/categories", CategoryDto.From(category));
    }

    /// <summary>Update a category. Only the owning Organiser may do this.</summary>
    [HttpPut("/api/categories/{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, CategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category is null) return NotFound();
        if (category.Event.OrganiserID != User.GetUserId()) return StatusCode(StatusCodes.Status403Forbidden);

        category.CategoryName = request.CategoryName.Trim();
        category.DistanceKM = request.DistanceKM;
        category.MaxParticipants = request.MaxParticipants;
        category.EntryFee = request.EntryFee;
        await _db.SaveChangesAsync();
        return Ok(CategoryDto.From(category));
    }

    /// <summary>Delete a category. Only the owning Organiser may do this; refused (409) if anyone has enrolled.</summary>
    [HttpDelete("/api/categories/{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).Include(c => c.Route)
            .FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category is null) return NotFound();
        if (category.Event.OrganiserID != User.GetUserId()) return StatusCode(StatusCodes.Status403Forbidden);
        if (await _db.Enrolments.AnyAsync(e => e.CategoryID == id))
            return Conflict(new { message = "This category has enrolments and cannot be deleted." });

        if (category.Route is not null) _db.Routes.Remove(category.Route);
        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
