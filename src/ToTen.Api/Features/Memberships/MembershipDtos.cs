namespace ToTen.Api.Features.Memberships;

public record InviteMemberRequest(Guid UserId, string Role);
public record MembershipResponse(Guid OrganizationId, Guid UserId, string Role);
