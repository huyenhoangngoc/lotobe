using LoTo.Domain.Entities;
using LoTo.Domain.Enums;
using LoTo.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;

namespace LoTo.Infrastructure.Services;

public class StripePaymentService : IPaymentService
{
    private readonly string _secretKey;
    private readonly string _webhookSecret;
    private readonly string _successUrl;
    private readonly string _cancelUrl;
    private readonly ITransactionRepository _transactionRepo;
    private readonly IUserRepository _userRepo;
    private readonly ILogger<StripePaymentService> _logger;

    private static readonly Dictionary<string, long> PlanPrices = new()
    {
        ["yearly"] = 50000, // VND (zero-decimal currency)
    };

    public StripePaymentService(
        IConfiguration config,
        ITransactionRepository transactionRepo,
        IUserRepository userRepo,
        ILogger<StripePaymentService> logger)
    {
        _secretKey = config["Stripe:SecretKey"] ?? throw new InvalidOperationException("Stripe:SecretKey not configured");
        _webhookSecret = config["Stripe:WebhookSecret"] ?? "";
        _successUrl = config["Stripe:SuccessUrl"] ?? "http://localhost:5173/premium/result?session_id={CHECKOUT_SESSION_ID}";
        _cancelUrl = config["Stripe:CancelUrl"] ?? "http://localhost:5173/premium";
        _transactionRepo = transactionRepo;
        _userRepo = userRepo;
        _logger = logger;

        StripeConfiguration.ApiKey = _secretKey;
    }

    public async Task<PaymentResult> CreateCheckoutSessionAsync(Guid userId, string planType, CancellationToken ct = default)
    {
        if (!PlanPrices.TryGetValue(planType, out var amount))
            throw new ArgumentException("Plan type khong hop le");

        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = ["card"],
            Mode = "payment",
            Currency = "vnd",
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "vnd",
                        UnitAmount = amount, // VND is zero-decimal
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Lo To Online Premium - Goi {planType}",
                            Description = "Nang cap Premium: toi da 60 nguoi choi moi phong, su dung 1 nam",
                        },
                    },
                    Quantity = 1,
                },
            ],
            SuccessUrl = _successUrl,
            CancelUrl = _cancelUrl,
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = userId.ToString(),
                ["planType"] = planType,
            },
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: ct);

        _logger.LogInformation("Stripe Checkout Session created: {SessionId} for user {UserId}", session.Id, userId);

        return new PaymentResult(session.Id, session.Url, amount, session.ExpiresAt);
    }

    public async Task<bool> HandleWebhookAsync(string payload, string signature, CancellationToken ct = default)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, _webhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed");
            return false;
        }

        if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
        {
            var session = stripeEvent.Data.Object as Session;
            if (session is null) return false;

            _logger.LogInformation("Stripe checkout.session.completed: {SessionId}", session.Id);

            var transaction = await _transactionRepo.GetByStripeSessionIdAsync(session.Id, ct);
            if (transaction is null)
            {
                _logger.LogWarning("Transaction not found for Stripe session {SessionId}", session.Id);
                return true; // Still return true to acknowledge the webhook
            }

            if (transaction.Status == TransactionStatus.Completed)
                return true; // Already processed

            transaction.Status = TransactionStatus.Completed;
            transaction.StripePaymentIntentId = session.PaymentIntentId;
            transaction.StripeResponse = payload;
            transaction.CompletedAt = DateTime.UtcNow;
            await _transactionRepo.UpdateAsync(transaction, ct);

            // Upgrade premium
            var user = await _userRepo.GetByIdAsync(transaction.UserId, ct);
            if (user is not null)
            {
                user.IsPremium = true;
                user.PremiumExpiresAt = DateTime.UtcNow.AddYears(1);
                await _userRepo.UpdateAsync(user, ct);
                _logger.LogInformation("User {UserId} upgraded to premium via Stripe webhook", user.Id);
            }
        }

        return true;
    }

    public async Task<QueryPaymentResult> QuerySessionAsync(string sessionId, CancellationToken ct = default)
    {
        var service = new SessionService();

        try
        {
            var session = await service.GetAsync(sessionId, cancellationToken: ct);

            if (session.PaymentStatus == "paid")
            {
                return new QueryPaymentResult(true, session.PaymentIntentId, null);
            }

            return new QueryPaymentResult(false, null, $"Payment status: {session.PaymentStatus}");
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe query session failed for {SessionId}", sessionId);
            return new QueryPaymentResult(false, null, ex.Message);
        }
    }
}
