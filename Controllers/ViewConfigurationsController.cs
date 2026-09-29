using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuireClone.Contracts;
using QuireClone.Data;
using QuireClone.Domain;

namespace QuireClone.Controllers;

[ApiController]
[Route("api/views")]
public sealed class ViewConfigurationsController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(ViewConfigurationSaveRequest request, CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(request, cancellationToken);
        if (error is not null) return BadRequest(new { error });
        var view = new ViewConfiguration
        {
            Name = request.Name.Trim(), Kind = request.Kind,
            SortOrder = (await db.ViewConfigurations.MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? -1) + 1,
            FiltersJson = JsonSerializer.Serialize(request.Filters),
            VisibleFieldsJson = JsonSerializer.Serialize(request.VisibleFields.Distinct()),
            GroupByField = request.GroupByField
        };
        db.ViewConfigurations.Add(view);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/views/{view.Id}", new { view.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, ViewConfigurationSaveRequest request, CancellationToken cancellationToken)
    {
        var view = await db.ViewConfigurations.FindAsync([id], cancellationToken);
        if (view is null) return NotFound();
        var error = await ValidateAsync(request, cancellationToken);
        if (error is not null) return BadRequest(new { error });
        view.Name = request.Name.Trim();
        view.Kind = request.Kind;
        view.FiltersJson = JsonSerializer.Serialize(request.Filters);
        view.VisibleFieldsJson = JsonSerializer.Serialize(request.VisibleFields.Distinct());
        view.GroupByField = request.GroupByField;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var view = await db.ViewConfigurations.FindAsync([id], cancellationToken);
        if (view is null) return NotFound();
        if (await db.ViewConfigurations.CountAsync(cancellationToken) == 1)
            return BadRequest(new { error = "Die letzte Ansicht kann nicht gelöscht werden." });
        db.ViewConfigurations.Remove(view);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<string?> ValidateAsync(ViewConfigurationSaveRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Bitte einen Namen eingeben.";
        if (!Enum.IsDefined(request.Kind)) return "Ungültige Darstellung.";
        if (request.Filters.Count > 20) return "Maximal 20 Filter pro Ansicht.";
        var fields = (await db.CustomFields.Select(x => x.Id).ToListAsync(cancellationToken))
            .Select(x => $"field:{x}").ToHashSet();
        fields.UnionWith(["title", "description", "status", "startDate", "dueDate"]);
        if (request.VisibleFields.Any(x => !fields.Contains(x)) ||
            request.Filters.Any(x => !fields.Contains(x.Field)))
            return "Ein ausgewähltes Feld existiert nicht.";
        if (request.GroupByField is not null &&
            (!fields.Contains(request.GroupByField) || request.Kind == ViewKind.Calendar))
            return "Das Gruppierungsfeld ist für diese Darstellung ungültig.";
        var operators = new HashSet<string> { "equals", "notEquals", "contains", "isEmpty",
            "isNotEmpty", "before", "after", "greaterThan", "lessThan" };
        if (request.Filters.Any(x => !operators.Contains(x.Operator) || x.Value is null || x.Value.Length > 500))
            return "Ein Filter ist ungültig.";
        return null;
    }
}
