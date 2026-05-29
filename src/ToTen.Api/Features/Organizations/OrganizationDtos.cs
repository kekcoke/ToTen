namespace ToTen.Api.Features.Organizations;

public record CreateOrganizationRequest(string Name, string Type); // Type: "Household", "Business"
public record OrganizationResponse(Guid Id, string Name, string Type);
