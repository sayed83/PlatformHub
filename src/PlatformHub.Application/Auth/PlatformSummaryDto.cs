namespace PlatformHub.Application.Auth;

public sealed record PlatformSummaryDto(
    int TotalUsers,
    int ActiveUsers,
    int TotalTenants,
    int ActiveTenants,
    int TotalProjects,
    int ActiveProjects);
