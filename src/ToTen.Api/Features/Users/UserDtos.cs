using System.ComponentModel.DataAnnotations;

namespace ToTen.Api.Features.Users;

public record UserResponse(Guid Id, string Email, string[] Roles);
public record UpdateUserRolesRequest([property: MinLength(1)] string[] Roles);
