using Microsoft.EntityFrameworkCore;
using QuireClone.Contracts;
using QuireClone.Data;
using QuireClone.Domain;

namespace QuireClone.Services;

public sealed class TimeEntryService(AppDbContext db)
{
    public const int MaxDescriptionLength = 500;

    // Entries from the first to the last day, both included, ordered by day and start time.
    public async Task<List<TimeEntryDto>> GetAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from) throw new DomainValidationException("Das Enddatum liegt vor dem Startdatum.");
        if (to.DayNumber - from.DayNumber > 366) throw new DomainValidationException("Es kann höchstens ein Jahr abgefragt werden.");
        var entries = await db.TimeEntries.AsNoTracking()
            .Where(x => x.Date >= from && x.Date <= to)
            .ToListAsync(cancellationToken);
        return entries.OrderBy(x => x.Date).ThenBy(x => x.Start).ThenBy(x => x.End).Select(ToDto).ToList();
    }

    public async Task<TimeEntryDto> CreateAsync(TimeEntrySaveRequest request, CancellationToken cancellationToken)
    {
        var entry = new TimeEntry();
        Apply(entry, request);
        db.TimeEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entry);
    }

    public async Task<TimeEntryDto> UpdateAsync(Guid id, TimeEntrySaveRequest request, CancellationToken cancellationToken)
    {
        var entry = await db.TimeEntries.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("Aufwand nicht gefunden.");
        Apply(entry, request);
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entry);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = await db.TimeEntries.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("Aufwand nicht gefunden.");
        db.TimeEntries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    // An entry lies within one day; overlapping entries are allowed.
    private static void Apply(TimeEntry entry, TimeEntrySaveRequest request)
    {
        var description = request.Description.Trim();
        if (description.Length == 0) throw new DomainValidationException("Bitte eine Beschreibung eingeben.");
        if (description.Length > MaxDescriptionLength)
            throw new DomainValidationException($"Die Beschreibung darf maximal {MaxDescriptionLength} Zeichen lang sein.");
        if (request.Date is not { } date) throw new DomainValidationException("Bitte einen Tag angeben.");
        if (request.Start is not { } start || request.End is not { } end)
            throw new DomainValidationException("Bitte Beginn und Ende angeben.");
        // Seconds are not shown anywhere, so they are not stored either.
        start = new TimeOnly(start.Hour, start.Minute);
        end = new TimeOnly(end.Hour, end.Minute);
        if (end <= start) throw new DomainValidationException("Das Ende muss nach dem Beginn liegen.");

        entry.Date = date;
        entry.Start = start;
        entry.End = end;
        entry.Description = description;
    }

    private static TimeEntryDto ToDto(TimeEntry entry) =>
        new(entry.Id, entry.Date, entry.Start, entry.End, entry.Description);
}
