namespace ToTen.Api.Models;

public class Organization
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? LegalName { get; set; }
    public string? TaxId { get; set; }
    public required string Type { get; set; } // Household, Business
    public string? Industry { get; set; }
    
    public DateTimeOffset DateCreated { get; set; }
    public DateTimeOffset? DateDeleted { get; set; }

    public ICollection<OrganizationMembership> Memberships { get; set; } = [];
    public ICollection<InventoryItem> InventoryItems { get; set; } = [];
    public ICollection<Location> Locations { get; set; } = [];
}

public class OrganizationMembership
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    
    public required string UserId { get; set; } // Keycloak Sub
}
