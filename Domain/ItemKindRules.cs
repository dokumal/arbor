namespace QuireClone.Domain;

/// <summary>
/// What each kind of workspace item may hold and how kinds may be combined.
/// The frontend mirrors some of these rules for the UI; the server checks them.
/// </summary>
public static class ItemKindRules
{
    // Only tasks have a status and a start and due date.
    public static bool HasSchedule(WorkspaceItemKind kind) => kind == WorkspaceItemKind.Task;

    // Folders hold only a title and a description.
    public static bool HasCustomFields(WorkspaceItemKind kind) => kind != WorkspaceItemKind.Folder;

    // Folders can only be placed at the top level or inside other folders.
    public static bool CanContain(WorkspaceItemKind? parentKind, WorkspaceItemKind childKind) =>
        childKind != WorkspaceItemKind.Folder || parentKind is null or WorkspaceItemKind.Folder;
}
