import { api } from './api.js';

const $ = id => document.getElementById(id);
let current = null;

function formatSize(bytes) {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toLocaleString('de-DE', { maximumFractionDigits: 1 })} KB`;
    return `${(bytes / 1024 / 1024).toLocaleString('de-DE', { maximumFractionDigits: 1 })} MB`;
}

function show(settings) {
    current = settings;
    $('database-path').value = settings.databasePath;
    $('images-directory').value = settings.imagesDirectory;
    $('database-resolved').textContent = settings.resolvedDatabasePath;
    $('database-size').textContent = formatSize(settings.databaseSize);
    $('images-resolved').textContent = settings.resolvedImagesDirectory;
    $('image-count').textContent = settings.imageCount === 1 ? '1 Bild' : `${settings.imageCount} Bilder`;
}

function setStatus(message, error = false) {
    $('settings-status').textContent = message;
    $('settings-status').classList.toggle('error', error);
    $('settings-status').hidden = !message;
}

$('settings-form').addEventListener('submit', async event => {
    event.preventDefault();
    const payload = { databasePath: $('database-path').value.trim(), imagesDirectory: $('images-directory').value.trim() };
    if (current && payload.databasePath === current.databasePath && payload.imagesDirectory === current.imagesDirectory) {
        setStatus('Keine Änderungen.');
        return;
    }
    if (!confirm('Speicherorte ändern? Die Datenbank bzw. die Bilder werden dabei an den neuen Ort verschoben.')) return;
    setStatus('');
    $('settings-save').disabled = true;
    try {
        const result = await api('/api/settings', { method: 'PUT', body: JSON.stringify(payload) });
        show(result.settings);
        setStatus(result.message);
    } catch (error) { setStatus(error.message, true); }
    finally { $('settings-save').disabled = false; }
});

$('settings-defaults').addEventListener('click', () => {
    if (!current) return;
    $('database-path').value = current.defaults.databasePath;
    $('images-directory').value = current.defaults.imagesDirectory;
    setStatus('Standardwerte eingetragen. Zum Übernehmen speichern.');
});

api('/api/settings').then(show).catch(error => setStatus(error.message, true));
