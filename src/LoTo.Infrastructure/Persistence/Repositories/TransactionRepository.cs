using Dapper;
using LoTo.Domain.Entities;
using LoTo.Domain.Enums;
using LoTo.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace LoTo.Infrastructure.Persistence.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly string _connectionString;

    public TransactionRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Supabase")
            ?? throw new InvalidOperationException("Supabase connection string not configured");
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<TransactionRow>(
            "SELECT * FROM transactions WHERE id = @Id", new { Id = id });
        return row?.ToEntity();
    }

    public async Task<Transaction?> GetByStripeSessionIdAsync(string sessionId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<TransactionRow>(
            "SELECT * FROM transactions WHERE stripe_session_id = @SessionId", new { SessionId = sessionId });
        return row?.ToEntity();
    }

    public async Task<Transaction> CreateAsync(Transaction transaction, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var id = await conn.QuerySingleAsync<Guid>("""
            INSERT INTO transactions (user_id, stripe_session_id, amount, plan_type, status)
            VALUES (@UserId, @StripeSessionId, @Amount, @PlanType, @Status)
            RETURNING id
            """,
            new
            {
                transaction.UserId,
                transaction.StripeSessionId,
                transaction.Amount,
                transaction.PlanType,
                Status = transaction.Status.ToString().ToLower(),
            });
        transaction.Id = id;
        return transaction;
    }

    public async Task UpdateAsync(Transaction transaction, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.ExecuteAsync("""
            UPDATE transactions SET
                stripe_payment_intent_id = @StripePaymentIntentId,
                status = @Status,
                stripe_response = @StripeResponse::jsonb,
                completed_at = @CompletedAt
            WHERE id = @Id
            """,
            new
            {
                transaction.Id,
                transaction.StripePaymentIntentId,
                Status = transaction.Status.ToString().ToLower(),
                transaction.StripeResponse,
                transaction.CompletedAt,
            });
    }

    public async Task<List<Transaction>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var rows = await conn.QueryAsync<TransactionRow>(
            "SELECT * FROM transactions WHERE user_id = @UserId ORDER BY created_at DESC",
            new { UserId = userId });
        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task<(List<Transaction> Items, int TotalCount)> GetAllAsync(int page, int pageSize, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var offset = (page - 1) * pageSize;
        var totalCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM transactions");
        var rows = await conn.QueryAsync<TransactionRow>(
            "SELECT * FROM transactions ORDER BY created_at DESC LIMIT @Limit OFFSET @Offset",
            new { Limit = pageSize, Offset = offset });
        return (rows.Select(r => r.ToEntity()).ToList(), totalCount);
    }

    private class TransactionRow
    {
        public Guid Id { get; set; }
        public Guid User_Id { get; set; }
        public string? Stripe_Session_Id { get; set; }
        public string? Stripe_Payment_Intent_Id { get; set; }
        public long Amount { get; set; }
        public string Plan_Type { get; set; } = "yearly";
        public string Status { get; set; } = "pending";
        public string? Stripe_Response { get; set; }
        public DateTime Created_At { get; set; }
        public DateTime? Completed_At { get; set; }

        public Transaction ToEntity() => new()
        {
            Id = Id,
            UserId = User_Id,
            StripeSessionId = Stripe_Session_Id,
            StripePaymentIntentId = Stripe_Payment_Intent_Id,
            Amount = Amount,
            PlanType = Plan_Type,
            Status = Enum.Parse<TransactionStatus>(Status, ignoreCase: true),
            StripeResponse = Stripe_Response,
            CreatedAt = Created_At,
            CompletedAt = Completed_At,
        };
    }
}
