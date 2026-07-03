using Tensu.Core.Enums;

namespace Tensu.Core.Entities;

public class User
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public UserRole Role { get; set; } = UserRole.Developer;
    public string AuthProvider { get; set; } = "local"; // local, oauth, oidc
    public string? ExternalId { get; set; }
    public string? PictureUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    // Navigation
    public Organization Organization { get; set; } = null!;
    public ICollection<ApiKey> ApiKeys { get; set; } = [];
}
