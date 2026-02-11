using Dapper;
using LoTo.Domain.Entities;
using LoTo.Domain.Enums;
using LoTo.Domain.Interfaces;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace LoTo.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Supabase")
            ?? throw new InvalidOperationException("Supabase connection string not configured");
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<UserRow>(
            "SELECT * FROM users WHERE id = @Id", new { Id = id });
        return row?.ToEntity();
    }

    public async Task<User?> GetByGoogleIdAsync(string googleId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<UserRow>(
            "SELECT * FROM users WHERE google_id = @GoogleId", new { GoogleId = googleId });
        return row?.ToEntity();
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<UserRow>(
            "SELECT * FROM users WHERE email = @Email", new { Email = email });
        return row?.ToEntity();
    }

    public async Task<User> CreateAsync(User user, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var id = await conn.QuerySingleAsync<Guid>("""
            INSERT INTO users (google_id, email, display_name, avatar_url, role, is_premium, is_banned)
            VALUES (@GoogleId, @Email, @DisplayName, @AvatarUrl, @Role, @IsPremium, @IsBanned)
            RETURNING id
            """,
            new
            {
                user.GoogleId,
                user.Email,
                user.DisplayName,
                user.AvatarUrl,
                Role = user.Role.ToString().ToLower(),
                user.IsPremium,
                user.IsBanned,
            });

        user.Id = id;
        return user;
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.ExecuteAsync("""
            UPDATE users SET
                display_name = @DisplayName,
                avatar_url = @AvatarUrl,
                is_premium = @IsPremium,
                premium_expires_at = @PremiumExpiresAt,
                is_banned = @IsBanned,
                terms_accepted_at = @TermsAcceptedAt,
                terms_version = @TermsVersion,
                updated_at = now()
            WHERE id = @Id
            """,
            new
            {
                user.Id,
                user.DisplayName,
                user.AvatarUrl,
                user.IsPremium,
                user.PremiumExpiresAt,
                user.IsBanned,
                user.TermsAcceptedAt,
                user.TermsVersion,
            });
    }

    public async Task<(List<User> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? search = null, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var offset = (page - 1) * pageSize;

        var where = string.IsNullOrWhiteSpace(search)
            ? ""
            : "WHERE display_name ILIKE @Search OR email ILIKE @Search";
        var searchParam = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%";

        var countSql = $"SELECT COUNT(*) FROM users {where}";
        var totalCount = await conn.ExecuteScalarAsync<int>(countSql, new { Search = searchParam });

        var dataSql = $"SELECT * FROM users {where} ORDER BY created_at DESC LIMIT @Limit OFFSET @Offset";
        var rows = await conn.QueryAsync<UserRow>(dataSql, new { Search = searchParam, Limit = pageSize, Offset = offset });

        return (rows.Select(r => r.ToEntity()).ToList(), totalCount);
    }

    // Internal mapping class for Dapper snake_case -> PascalCase
    private class UserRow
    {
        public Guid Id { get; set; }
        public string? Google_Id { get; set; }
        public string? Email { get; set; }
        public string Display_Name { get; set; } = "";
        public string? Avatar_Url { get; set; }
        public string Role { get; set; } = "host";
        public bool Is_Premium { get; set; }
        public DateTime? Premium_Expires_At { get; set; }
        public bool Is_Banned { get; set; }
        public DateTime? Terms_Accepted_At { get; set; }
        public string? Terms_Version { get; set; }
        public DateTime Created_At { get; set; }
        public DateTime Updated_At { get; set; }

        public User ToEntity() => new()
        {
            Id = Id,
            GoogleId = Google_Id,
            Email = Email,
            DisplayName = Display_Name,
            AvatarUrl = Avatar_Url,
            Role = Enum.Parse<UserRole>(Role, ignoreCase: true),
            IsPremium = Is_Premium,
            PremiumExpiresAt = Premium_Expires_At,
            IsBanned = Is_Banned,
            TermsAcceptedAt = Terms_Accepted_At,
            TermsVersion = Terms_Version,
            CreatedAt = Created_At,
            UpdatedAt = Updated_At,
        };
    }
}
