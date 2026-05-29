namespace ToTen.Api.Models;

public class Manifest
{
    public Guid Id { get; set; }
    
    public Guid SourceLocationId { get; set; }
    public Location? SourceLocation { get; set; }

    public Guid DestinationLocationId { get; set; }
    public Location? DestinationLocation { get; set; }

    public ManifestStatus Status { get; set; }

    // RBA
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

public enum ManifestStatus
{
    Draft,
    Pending,
    InTransit,
    Received,
    Cancelled
}
