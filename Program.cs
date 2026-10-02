using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NorthwindApi.Data;
using NorthwindApi.Services;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// CONFIGURACIÓN DE SERVICIOS (DI Container)
// ==========================================

// --- DbContext: Registro seguro con pooling y configuración de resiliencia ---
builder.Services.AddDbContext<NorthwindDbContext>(options =>
{
    var connString = builder.Configuration.GetConnectionString("Northwind")
        ?? throw new InvalidOperationException("Connection string 'Northwind' not found.");

    options.UseSqlServer(connString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
        sqlOptions.CommandTimeout(30);
    });

    // Solo en Development: logging sensible de SQL
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging(false); // NUNCA true en producción
        options.EnableDetailedErrors();
    }
});

// --- Inyección de dependencias: Servicios (Scoped = por request) ---
builder.Services.AddScoped<IOrderService, OrderService>();

// --- Autenticación JWT Bearer (REQUISITO 1) ---
// VALIDACIÓN SEGURA DE CLAIMS:
// - ValidateIssuer: Verifica que el token venga de una autoridad confiable (Issuer)
// - ValidateAudience: Verifica que el token sea para esta API (Audience)
// - ValidateIssuerSigningKey: Verifica la firma criptográfica con la clave secreta
// - ValidateLifetime: Verifica expiración (exp) y not-before (nbf)
// - ClockSkew: Tolerancia de reloj (default 5 min) para diferencias entre servidores
var jwtConfig = builder.Configuration.GetSection("Jwt");
var signingKey = jwtConfig["SigningKey"] ?? throw new InvalidOperationException("JWT SigningKey not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtConfig["Issuer"],

            ValidateAudience = true,
            ValidAudience = jwtConfig["Audience"],

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(jwtConfig.GetValue<int>("ClockSkewMinutes", 5)),

            // Requiere claims específicos para mayor seguridad
            RequireExpirationTime = true,
            RequireSignedTokens = true
        };

        // Eventos para logging de seguridad (auditoría)
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("JWT Authentication failed: {Message}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                // Aquí se pueden validar claims adicionales custom (ej. "scope", "permissions")
                return Task.CompletedTask;
            }
        };
    });

// --- Autorización: Políticas basadas en roles (RBAC) ---
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SalesOrManager", policy =>
        policy.RequireRole("SalesAgent", "Manager"));

    options.AddPolicy("ManagerOnly", policy =>
        policy.RequireRole("Manager"));

    // Política por defecto: usuario autenticado
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// --- CORS RESTRICITIVO (REQUISITO 4) ---
// NUNCA usar AllowAnyOrigin() con AllowCredentials() = vulnerabilidad crítica
// Configuración explícita desde appsettings.json
var corsConfig = builder.Configuration.GetSection("Cors");
builder.Services.AddCors(options =>
{
    options.AddPolicy("NorthwindCorsPolicy", policy =>
    {
        var allowedOrigins = corsConfig.GetSection("AllowedOrigins").Get<string[]>() ?? [];
        var allowedMethods = corsConfig.GetSection("AllowedMethods").Get<string[]>() ?? ["GET", "POST"];
        var allowedHeaders = corsConfig.GetSection("AllowedHeaders").Get<string[]>() ?? ["Authorization", "Content-Type"];
        var allowCredentials = corsConfig.GetValue<bool>("AllowCredentials", false);
        var maxAge = corsConfig.GetValue<int>("MaxAgeSeconds", 3600);

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .WithMethods(allowedMethods)
                  .WithHeaders(allowedHeaders)
                  .SetPreflightMaxAge(TimeSpan.FromSeconds(maxAge));

            if (allowCredentials)
            {
                policy.AllowCredentials();
            }
        }
        else
        {
            // Fallback seguro: denegar todo si no hay configuración
            policy.WithOrigins("https://invalid-origin-placeholder.local");
        }
    });
});

// --- RATE LIMITING NATIVO (REQUISITO 4) ---
// Mitiga Fuerza Bruta, DoS, scraping
// Políticas diferenciadas por criticidad del endpoint
builder.Services.AddRateLimiter(options =>
{
    var rlConfig = builder.Configuration.GetSection("RateLimiting");

    // Política por defecto para endpoints generales
    options.AddFixedWindowLimiter("default", opt =>
    {
        var cfg = rlConfig.GetSection("DefaultPolicy");
        opt.PermitLimit = cfg.GetValue<int>("PermitLimit", 100);
        opt.Window = TimeSpan.FromSeconds(cfg.GetValue<int>("WindowSeconds", 60));
        opt.QueueLimit = cfg.GetValue<int>("QueueLimit", 10);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Política estricta para endpoints de autenticación/login
    options.AddFixedWindowLimiter("auth", opt =>
    {
        var cfg = rlConfig.GetSection("AuthPolicy");
        opt.PermitLimit = cfg.GetValue<int>("PermitLimit", 20);
        opt.Window = TimeSpan.FromSeconds(cfg.GetValue<int>("WindowSeconds", 60));
        opt.QueueLimit = cfg.GetValue<int>("QueueLimit", 5);
    });

    // Política muy estricta para creación de órdenes (escritura)
    options.AddFixedWindowLimiter("create-order", opt =>
    {
        var cfg = rlConfig.GetSection("CreateOrderPolicy");
        opt.PermitLimit = cfg.GetValue<int>("PermitLimit", 10);
        opt.Window = TimeSpan.FromSeconds(cfg.GetValue<int>("WindowSeconds", 60));
        opt.QueueLimit = cfg.GetValue<int>("QueueLimit", 3);
    });

    // Respuesta personalizada cuando se excede el límite
    options.OnRejected = (context, token) =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("Rate limit exceeded for {IP} on {Path}",
            context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            context.HttpContext.Request.Path);

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "60";

        return ValueTask.CompletedTask;
    };
});

// --- Controllers con validación automática de ModelState ---
// ApiBehaviorOptions: Personaliza respuesta 400 automática
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Deshabilita la validación automática de ModelState (la hacemos manual para control total)
        // options.SuppressModelStateInvalidFilter = true;

        // Personaliza el formato del error 400 (ProblemDetails)
        options.InvalidModelStateResponseFactory = context =>
        {
            var problemDetails = new ValidationProblemDetails(context.ModelState)
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Uno o más errores de validación ocurrieron.",
                Status = StatusCodes.Status400BadRequest,
                Instance = context.HttpContext.Request.Path
            };

            return new BadRequestObjectResult(problemDetails)
            {
                ContentTypes = { "application/problem+json", "application/json" }
            };
        };
    });

// --- Swagger/OpenAPI con configuración de seguridad ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Northwind API",
        Version = "v1",
        Description = "API segura para gestión de órdenes Northwind"
    });

    // Configuración JWT en Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese 'Bearer {token}' para autenticarse."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// --- ProblemDetails global (REQUISITO 4) ---
// Estandariza respuestas de error (RFC 7807)
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Instance = ctx.HttpContext.Request.Path;

        // En producción: NUNCA incluir detalles de excepción (stack trace)
        if (!builder.Environment.IsDevelopment())
        {
            ctx.ProblemDetails.Detail = "Ocurrió un error interno. Contacte al administrador.";
        }
    };
});

// ==========================================
// CONSTRUCCIÓN DE LA APLICACIÓN
// ==========================================
var app = builder.Build();

// ==========================================
// MIDDLEWARE PIPELINE (ORDEN CRÍTICO)
// ==========================================

// 1. Manejo global de excepciones (REQUISITO 4)
// DEBE ir primero para capturar todo lo posterior
app.UseExceptionHandler("/error");

// Endpoint de error que devuelve ProblemDetails genérico
app.Map("/error", (HttpContext context) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;

    logger.LogError(exception, "Excepción no manejada: {Message}", exception?.Message);

    var problemDetails = new ProblemDetails
    {
        Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
        Title = "Error interno del servidor",
        Status = StatusCodes.Status500InternalServerError,
        Detail = builder.Environment.IsDevelopment()
            ? exception?.ToString() // Solo en Development
            : "Ocurrió un error inesperado. Por favor intente más tarde.",
        Instance = context.Request.Path
    };

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    return Results.Problem(problemDetails);
}).ExcludeFromDescription();

// 2. HTTPS Redirection (seguridad en tránsito)
app.UseHttpsRedirection();

// 3. CORS (antes de Auth)
app.UseCors("NorthwindCorsPolicy");

// 4. Rate Limiting (antes de Auth para proteger endpoint de login también)
app.UseRateLimiter();

// 5. Autenticación y Autorización
app.UseAuthentication();
app.UseAuthorization();

// 6. Swagger solo en Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Northwind API v1");
        c.RoutePrefix = "swagger";
    });
}

// 7. Mapeo de controladores
app.MapControllers();

// Health check endpoint (sin autenticación para monitoring)
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .ExcludeFromDescription()
   .AllowAnonymous();

// Migración automática de BD en Development (opcional)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NorthwindDbContext>();
    db.Database.Migrate();
}

app.Run();

// Hacer Program accesible para testing (WebApplicationFactory)
public partial class Program { }