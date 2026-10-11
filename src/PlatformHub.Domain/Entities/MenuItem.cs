namespace PlatformHub.Domain.Entities;

/// <summary>A project menu item. ParentMenuId supports nested sidebar menus.</summary>
public sealed class MenuItem
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public long? ModuleId { get; set; }
    public ProjectModule? Module { get; set; } = null!;
    public long? ParentMenuId { get; set; }
    public MenuItem? ParentMenu { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Area { get; set; }
    public string? Controller { get; set; }
    public string? Action { get; set; }
    public string? RouteUrl { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsExternal { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
