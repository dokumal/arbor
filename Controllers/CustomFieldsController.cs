using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuireClone.Contracts;
using QuireClone.Data;
using QuireClone.Services;

namespace QuireClone.Controllers;

[ApiController]
[Route("api/fields")]
public sealed class CustomFieldsController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CustomFieldSaveRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length == 0) return BadRequest(new { error = "Bitte einen Feldnamen eingeben." });
        if (!Enum.IsDefined(request.Type)) return BadRequest(new { error = "Ungültiger Feldtyp." });
        if (await db.CustomFields.AnyAsync(x => x.Name.ToLower() == name.ToLower(), cancellationToken))
            return BadRequest(new { error = "Ein Feld mit diesem Namen existiert bereits." });
        var options = request.Options.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToList();
        if (request.Type == Domain.CustomFieldType.Select && options.Count == 0)
            return BadRequest(new { error = "Für ein Auswahlfeld sind Optionen erforderlich." });
        var field = new Domain.CustomFieldDefinition
        {
            Name = name, Type = request.Type,
            OptionsJson = JsonSerializer.Serialize(options),
            SortOrder = (await db.CustomFields.MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? -1) + 1
        };
        db.CustomFields.Add(field);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/fields/{field.Id}", new { field.Id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var field = await db.CustomFields.FindAsync([id], cancellationToken);
        if (field is null) return NotFound();
        var key = $"field:{id}";
        foreach (var view in await db.ViewConfigurations.ToListAsync(cancellationToken))
        {
            var visible = WorkspaceService.Deserialize<List<string>>(view.VisibleFieldsJson);
            var filters = WorkspaceService.Deserialize<List<ViewFilter>>(view.FiltersJson);
            view.VisibleFieldsJson = JsonSerializer.Serialize(visible.Where(x => x != key));
            view.FiltersJson = JsonSerializer.Serialize(filters.Where(x => x.Field != key));
            if (view.GroupByField == key) view.GroupByField = null;
        }
        db.CustomFields.Remove(field);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
