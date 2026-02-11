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

    public async Task<Transaction?> GetByMomoOrderIdAsync(string momoOrderId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<TransactionRow>(
            "SELECT * FROM transactions WHERE momo_order_id = @MomoOrderId", new { MomoOrderId = momoOrderId });
        return row?.ToEntity();
    }

    public async Task<Transaction> CreateAsync(Transaction transaction, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var id = await conn.QuerySingleAsync<Guid>("""
            INSERT INTO transactions (user_id, momo_order_id, amount, plan_type, status)
            VALUES (@UserId, @MomoOrderId, @Amount, @PlanType, @Status)
            RETURNING id
            """,
            new
            {
                transaction.UserId,
                transaction.MomoOrderId,
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
                momo_trans_id = @MomoTransId,
                status = @Status,
                momo_response = @MomoResponse::jsonb,
                completed_at = @CompletedAt
            WHERE id = @Id
            """,
            new
            {
                transaction.Id,
                transaction.MomoTransId,
                Status = transaction.Status.ToString().ToLower(),
                transaction.MomoResponse,
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
        public string? Momo_Order_Id { get; set; }
        public string? Momo_Trans_Id { get; set; }
        public long Amount { get; set; }
        public string Plan_Type { get; set; } = "monthly";
        public string Status { get; set; } = "pending";
        public string? Momo_Response { get; set; }
        public DateTime Created_At { get; set; }
        public DateTime? Completed_At { get; set; }

        public Transaction ToEntity() => new()
        {
            Id = Id,
            UserId = User_Id,
            MomoOrderId = Momo_Order_Id,
            MomoTransId = Momo_Trans_Id,
            Amount = Amount,
            PlanType = Plan_Type,
            Status = Enum.Parse<TransactionStatus>(Status, ignoreCase: true),
            MomoResponse = Momo_Response,
            CreatedAt = Created_At,
            CompletedAt = Completed_At,
        };
    }
}
