namespace GekkoSuite.Api.Entities;

public class PermissionEntity
{
    /// <summary>The permission's id.</summary>
    public Guid PermissionId { get; set; }

    /// <summary>The thing acted on, e.g. "product", "sale", "role".</summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>What may be done to it, e.g. "read", "create", "refund".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>True for a company-level permission that may only sit in an ORGANIZATION role.</summary>
    public bool IsElevated { get; set; }
}
