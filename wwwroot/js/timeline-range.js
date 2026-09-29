export const timelineIntervals = { Week: 'Woche', Month: 'Monat', ThreeMonths: '3 Monate' };
const dayMs = 86400000;
const iso = date => date.toISOString().slice(0, 10);
const shortDay = new Intl.DateTimeFormat('de-DE', { weekday: 'short', day: '2-digit', timeZone: 'UTC' });
const monthLabel = new Intl.DateTimeFormat('de-DE', { month: 'long', year: 'numeric', timeZone: 'UTC' });

export function getTimelineRange(anchor, interval) {
    const start = new Date(`${anchor}T00:00:00Z`);
    if (interval === 'Week') start.setUTCDate(start.getUTCDate() - (start.getUTCDay() + 6) % 7);
    else start.setUTCDate(1);
    const end = new Date(start);
    if (interval === 'Week') end.setUTCDate(end.getUTCDate() + 7);
    else end.setUTCMonth(end.getUTCMonth() + (interval === 'ThreeMonths' ? 3 : 1));
    const days = (end - start) / dayMs;
    const columns = [];
    if (interval === 'ThreeMonths') {
        for (let index = 0; index < 3; index++) {
            const from = new Date(start); from.setUTCMonth(from.getUTCMonth() + index);
            const to = new Date(from); to.setUTCMonth(to.getUTCMonth() + 1);
            columns.push({ label: monthLabel.format(from), days: (to - from) / dayMs });
        }
    } else {
        for (let index = 0; index < days; index++) {
            const date = new Date(start); date.setUTCDate(date.getUTCDate() + index);
            columns.push({ label: interval === 'Week' ? shortDay.format(date) : String(date.getUTCDate()).padStart(2, '0'), days: 1 });
        }
    }
    return { start: iso(start), end: iso(end), days, columns };
}

export function shiftTimelineDate(anchor, interval, amount) {
    const date = new Date(`${getTimelineRange(anchor, interval).start}T00:00:00Z`);
    if (interval === 'Week') date.setUTCDate(date.getUTCDate() + amount * 7);
    else date.setUTCMonth(date.getUTCMonth() + amount * (interval === 'ThreeMonths' ? 3 : 1));
    return iso(date);
}
