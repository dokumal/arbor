export async function api(path, options = {}) {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const response = await fetch(path, {
        ...options,
        headers: {
            'Content-Type': 'application/json',
            ...(token ? { RequestVerificationToken: token } : {}),
            ...(options.headers || {})
        }
    });
    if (!response.ok) {
        let message = `Anfrage fehlgeschlagen (${response.status}).`;
        try {
            const error = await response.json();
            message = error.error || error.title || Object.values(error.errors || {}).flat().join(' ') || message;
        } catch { /* A plain error response still gets a useful status. */ }
        throw new Error(message);
    }
    if (response.status === 204) return null;
    return response.json();
}
