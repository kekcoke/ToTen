using System.Text.Json;
using NetTopologySuite.Geometries;

namespace ToTen.Api.Models;

public class Location
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    
    // PostGIS
    public Point? Coordinates { get; set; }
    
    // RBA
    public required string OwnerId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public JsonDocument? Metadata { get; set; }
}

public class Box
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    
    public Guid LocationId { get; set; }
    public Location? Location { get; set; }

    // RBA
    public required string OwnerId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public Guid? ManifestId { get; set; }
    public Manifest? Manifest { get; set; }
}
