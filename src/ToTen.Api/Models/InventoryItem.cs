using System.Text.Json;
using NetTopologySuite.Geometries;

namespace ToTen.Api.Models;

public class InventoryItem
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public Category? Category { get; set; }

    public Guid CategoryId { get; set; }

    public required string Description { get; set; }

    public required string LastUpdatedBy { get; set; }

    // RBA
    public required string OwnerId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    // Storage Slice
    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }

    public Guid? BoxId { get; set; }
    public Box? Box { get; set; }

    // Extensible Attributes
    public JsonDocument? Attributes { get; set; }
}
