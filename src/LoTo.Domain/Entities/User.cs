namespace LoTo.Domain.Entities;

using LoTo.Domain.Enums;

public class User
{
    public Guid Id { get; set; }
    public string? GoogleId { get; set; }
    public string? Email { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.Host;
    public bool IsPremium { get; set; }
    public DateTime? PremiumExpiresAt { get; set; }
    public bool IsBanned { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
