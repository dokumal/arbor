// Date and time helpers for the week view. Dates are "YYYY-MM-DD" strings, times are minutes since midnight.
const dayMs = 86400000;
export const workdayStart = 8 * 60;
export const workdayEnd = 18 * 60;
export const snapMinutes = 15;
export const dayMinutes = 24 * 60;

export const dayNumber = date => Date.parse(`${date}T00:00:00Z`) / dayMs;
export const dateString = day => new Date(day * dayMs).toISOString().slice(0, 10);
export const addDays = (date, days) => dateString(dayNumber(date) + days);
// 0 is Monday, 6 is Sunday.
export const weekdayIndex = date => (new Date(`${date}T00:00:00Z`).getUTCDay() + 6) % 7;
export const mondayOf = date => addDays(date, -weekdayIndex(date));

export function localToday() {
    const now = new Date();
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
}

// ISO 8601: week 1 is the week with the year's first Thursday.
export function isoWeek(date) {
    const thursday = dayNumber(mondayOf(date)) + 3;
    const year = new Date(thursday * dayMs).getUTCFullYear();
    return Math.floor((thursday - dayNumber(`${year}-01-01`)) / 7) + 1;
}

const longDate = new Intl.DateTimeFormat('de-DE', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' });
const dayMonth = new Intl.DateTimeFormat('de-DE', { day: 'numeric', month: 'long', timeZone: 'UTC' });
const utc = date => new Date(`${date}T00:00:00Z`);

export function weekLabel(monday) {
    const sunday = addDays(monday, 6);
    const first = monday.slice(0, 4) !== sunday.slice(0, 4) ? longDate.format(utc(monday))
        : monday.slice(5, 7) !== sunday.slice(5, 7) ? dayMonth.format(utc(monday))
        : `${utc(monday).getUTCDate()}.`;
    return `KW ${isoWeek(monday)} · ${first} – ${longDate.format(utc(sunday))}`;
}

export const toMinutes = time => Number(time.slice(0, 2)) * 60 + Number(time.slice(3, 5));
export const formatTime = minutes => `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;
// A time of day for the API; the end of the day is stored as 23:59.
export const apiTime = minutes => `${formatTime(Math.min(minutes, dayMinutes - 1))}:00`;
export const formatDuration = minutes => `${Math.floor(minutes / 60)}:${String(minutes % 60).padStart(2, '0')} h`;
export const snap = minutes => Math.round(minutes / snapMinutes) * snapMinutes;
export const entryMinutes = entry => toMinutes(entry.end) - toMinutes(entry.start);

// The working day, widened to whole hours so that every entry is visible.
export function visibleRange(entries) {
    let start = workdayStart, end = workdayEnd;
    for (const entry of entries) {
        start = Math.min(start, Math.floor(toMinutes(entry.start) / 60) * 60);
        end = Math.max(end, Math.ceil(toMinutes(entry.end) / 60) * 60);
    }
    return { start, end };
}

// Overlapping entries share the width of the day: each gets a column within its group of overlapping entries.
export function layoutDay(entries) {
    const items = entries.map(entry => ({ entry, start: toMinutes(entry.start), end: toMinutes(entry.end), column: 0, columns: 1 }))
        .sort((a, b) => a.start - b.start || b.end - a.end);
    let group = [], columnEnds = [], groupEnd = -1;
    const closeGroup = () => {
        group.forEach(item => { item.columns = columnEnds.length; });
        group = []; columnEnds = [];
    };
    for (const item of items) {
        if (group.length && item.start >= groupEnd) closeGroup();
        let column = columnEnds.findIndex(end => end <= item.start);
        if (column < 0) column = columnEnds.length;
        columnEnds[column] = item.end;
        item.column = column;
        group.push(item);
        groupEnd = Math.max(groupEnd, item.end);
    }
    closeGroup();
    return items;
}
