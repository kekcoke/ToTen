using System.ComponentModel.DataAnnotations;

namespace ToTen.Api.Features.Organizations;

public record CreateOrganizationRequest(
    [property: Required, StringLength(200)] string Name,
    [property: RegularExpression("^(Household|Business)$")] string Type);
public record OrganizationResponse(Guid Id, string Name, string Type);
