using System.Text;
using LoTo.Application.Interfaces;
using LoTo.Application.UseCases.Auth;
using LoTo.Application.UseCases.Games;
using LoTo.Application.UseCases.Rooms;
using LoTo.Domain.Interfaces;
using LoTo.Infrastructure.Persistence.Repositories;
using LoTo.Infrastructure.Services;
using LoTo.Infrastructure.SignalR.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Services
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "dev-secret-key-min-32-characters!!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "LoToOnline";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtIssuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        };
    });
builder.Services.AddAuthorization();

// DI: Application services
builder.Services.AddScoped<GoogleLoginUseCase>();
builder.Services.AddScoped<JoinRoomUseCase>();
builder.Services.AddScoped<CreateRoomUseCase>();
builder.Services.AddScoped<StartGameUseCase>();
builder.Services.AddScoped<DrawNumberUseCase>();
builder.Services.AddScoped<ClaimKinhUseCase>();
builder.Services.AddScoped<EndGameUseCase>();

// DI: Infrastructure services
builder.Services.AddSingleton<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<IConnectionMapping, ConnectionMapping>();
builder.Services.AddSingleton<IRoomGameSettings, RoomGameSettingsService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IRoomPlayerRepository, RoomPlayerRepository>();
builder.Services.AddScoped<IGameSessionRepository, GameSessionRepository>();
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<IDrawnNumberRepository, DrawnNumberRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IAppSettingsRepository, AppSettingsRepository>();

// MoMo Payment
builder.Services.AddHttpClient<IPaymentService, MoMoPaymentService>();

// Health checks
var connectionString = builder.Configuration.GetConnectionString("Supabase");
builder.Services.AddHealthChecks();
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "database");
}

// CORS
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Lo To Online API v1");
    });
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<GameHub>("/hubs/game");

app.MapHealthChecks("/health");

Log.Information("Lo To Online API starting...");
app.Run();
