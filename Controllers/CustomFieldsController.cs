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
        if (options.Any(x => x.Length > 500))
            return BadRequest(new { error = "Optionen dürfen maximal 500 Zeichen lang sein." });
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

    [HttpPut("{id:guid}/options")]
    public async Task<IActionResult> UpdateOptions(Guid id, CustomFieldOptionsRequest request, CancellationToken cancellationToken)
    {
        var field = await db.CustomFields.FindAsync([id], cancellationToken);
        if (field is null) return NotFound();
        if (field.Type != Domain.CustomFieldType.Select)
            return BadRequest(new { error = "Nur Auswahlfelder haben Optionen." });
        var current = JsonSerializer.Deserialize<List<string>>(field.OptionsJson)!;
        var changes = request.Options.Select(x => new { Original = x.Original, Value = x.Value.Trim() }).ToList();
        if (changes.Count == 0)
            return BadRequest(new { error = "Für ein Auswahlfeld sind Optionen erforderlich." });
        if (changes.Any(x => x.Value.Length is 0 or > 500))
            return BadRequest(new { error = "Optionen müssen 1 bis 500 Zeichen enthalten." });
        if (changes.Select(x => x.Value).Distinct().Count() != changes.Count)
            return BadRequest(new { error = "Jede Option darf nur einmal vorkommen." });
        var originals = changes.Where(x => x.Original is not null).Select(x => x.Original!).ToList();
        if (originals.Any(x => !current.Contains(x)) || originals.Distinct().Count() != originals.Count)
            return BadRequest(new { error = "Die Optionen wurden inzwischen geändert. Bitte lade die Seite neu." });

        // Renamed options keep their stored values; removed options lose them.
        var renames = changes.Where(x => x.Original is not null && x.Original != x.Value)
            .ToDictionary(x => x.Original!, x => x.Value);
        var removed = current.Except(originals).ToHashSet();
        if (renames.Count > 0 || removed.Count > 0)
        {
            foreach (var value in await db.ItemFieldValues.Where(x => x.FieldId == id).ToListAsync(cancellationToken))
            {
                if (removed.Contains(value.Value)) db.ItemFieldValues.Remove(value);
                else if (renames.TryGetValue(value.Value, out var renamed)) value.Value = renamed;
            }
            var key = $"field:{id}";
            foreach (var view in await db.ViewConfigurations.ToListAsync(cancellationToken))
            {
                var filters = WorkspaceService.Deserialize<List<ViewFilter>>(view.FiltersJson);
                if (!filters.Any(x => x.Field == key && renames.ContainsKey(x.Value))) continue;
                view.FiltersJson = JsonSerializer.Serialize(filters.Select(x =>
                    x.Field == key && renames.TryGetValue(x.Value, out var renamed) ? x with { Value = renamed } : x));
            }
        }
        field.OptionsJson = JsonSerializer.Serialize(changes.Select(x => x.Value));
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
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
            if (view.SortField == key) view.SortField = null;
        }
        db.CustomFields.Remove(field);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
