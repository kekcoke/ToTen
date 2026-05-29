namespace ToTen.Api.Features.Users;

public record UserResponse(Guid Id, string Email, string[] Roles);
public record UpdateUserRolesRequest(string[] Roles);
