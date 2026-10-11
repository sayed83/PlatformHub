namespace PlatformHub.Mvc.Models;

public sealed class WorkspaceMenuItemViewModel
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public long? ModuleId { get; set; }
    public string? ModuleName { get; set; }
    public long? ParentMenuId { get; set; }
    public string? Area { get; set; }
    public string? Controller { get; set; }
    public string? Action { get; set; }
    public string? RouteUrl { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsExternal { get; set; }
}
