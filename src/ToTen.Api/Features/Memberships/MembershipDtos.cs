using System.ComponentModel.DataAnnotations;
using ToTen.Api.Shared.Validation;

namespace ToTen.Api.Features.Memberships;

public record InviteMemberRequest(
    [property: NotEmptyGuid] Guid UserId,
    [property: RegularExpression("^(?i:Owner|Member)$")] string Role);
public record MembershipResponse(Guid OrganizationId, Guid UserId, string Role);
