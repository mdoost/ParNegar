using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ParNegar.API.Middleware;
using ParNegar.Application.Common;
using ParNegar.Application.Interfaces.Services;
using ParNegar.Application.Interfaces.Services.Auth;
using ParNegar.Application.Interfaces.Services.Core;
using ParNegar.Domain.Interfaces;
using ParNegar.Infrastructure.Data;
using ParNegar.Infrastructure.Repositories;
using ParNegar.Infrastructure.Services;
using ParNegar.Infrastructure.Services.Auth;
using ParNegar.Infrastructure.Services.Core;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ===== Serilog Configuration =====
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ParNegar")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .WriteTo.Conditional(
        _ => builder.Configuration.GetValue<bool>("Seq:Enabled"),
        writeTo => writeTo.Seq(builder.Configuration["Seq:ServerUrl"]!))
    .CreateLogger();

builder.Host.UseSerilog();

// ===== Database Configuration =====
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));

    if (builder.Configuration.GetValue<bool>("EFCoreLogging:EnableSensitiveDataLogging"))
    {
        options.EnableSensitiveDataLogging();
    }

    if (builder.Configuration.GetValue<bool>("EFCoreLogging:EnableDetailedErrors"))
    {
        options.EnableDetailedErrors();
    }
});

// ===== CORS Configuration =====
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ===== JWT Authentication =====
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false; // Set to true in production
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// ===== Dependency Injection =====
builder.Services.AddHttpContextAccessor();

// Memory Cache
builder.Services.AddMemoryCache();

// Infrastructure Services
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IDateTime, DateTimeService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

// Application Services (Auth Schema)
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Application Services (Core Schema)
builder.Services.AddScoped<IBranchService, BranchService>();

// Mapster (uses default global config)
// TypeAdapterConfig.GlobalSettings.Default.PreserveReference(true);

// ===== Controllers & Swagger =====
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ParNegar API",
        Version = "v1",
        Description = "ParNegar Backend API - Database-First with Clean Architecture"
    });

    // JWT Configuration for Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token (without 'Bearer' prefix)"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ===== Build App =====
var app = builder.Build();

// ===== Initialize Cache Manager =====
using (var scope = app.Services.CreateScope())
{
    var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
    CacheManager.Initialize(cacheService);
}

// ===== Middleware Pipeline =====
// ⚠️ ترتیب Middleware ها بسیار مهم است!

// 1. Request Logging - اولین middleware برای لاگ کامل درخواست‌ها
app.UseRequestLogging();

// 2. Global Exception Handler - برای گرفتن تمام خطاها
app.UseGlobalExceptionHandler();

// 3. Session Validation - بررسی Token Blacklist قبل از Authentication
app.UseSessionValidation();

// 4. File Upload Validation - اعتبارسنجی امنیتی فایل‌های آپلود شده
app.UseFileUploadValidation();

// 5. File Upload Rate Limiting - محدودسازی تعداد آپلود
app.UseFileUploadRateLimiting();

// 6. Swagger
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "ParNegar API v1");
    options.RoutePrefix = string.Empty; // Set Swagger UI at root (/)
    options.DocumentTitle = "ParNegar API";
    options.DisplayRequestDuration();
    options.EnableTryItOutByDefault();
});

// 7. Serilog Request Logging (ساده‌تر از RequestLoggingMiddleware)
app.UseSerilogRequestLogging();

// 8. HTTPS Redirection
app.UseHttpsRedirection();

// 9. CORS
app.UseCors();

// 10. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    timestamp = DateTimeOffset.UtcNow,
    environment = app.Environment.EnvironmentName
})).WithName("HealthCheck");

Log.Information("ParNegar API starting...");
app.Run();
Log.Information("ParNegar API stopped.");
