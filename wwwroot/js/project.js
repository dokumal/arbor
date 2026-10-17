// The project shown on the workspace page. Lists and new entries, fields, views and statuses go to its API paths;
// existing entries are addressed by their own id.
// Only the workspace page's <main> names the project; other pages have none.
export const projectId = document.querySelector('main[data-project-id]')?.dataset.projectId ?? null;
export const projectPath = path => `/api/projects/${projectId}${path}`;
// Browser settings that refer to ids of one project, e.g. the last selected view.
export const projectKey = key => `${key}:${projectId}`;
