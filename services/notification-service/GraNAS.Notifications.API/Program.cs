using System;
using System.Linq;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using GraNAS.Notifications.API.Hubs;
using GraNAS.Notifications.API.HostedServices;
using GraNAS.Notifications.API.Infrastructure;
using GraNAS.Notifications.DAL;
using GraNAS.Notifications.DAL.Extensions;
using GraNAS.Shared.Infrastructure.Extensions;
using GraNAS.Notifications.Services.Extensions;
using GraNAS.Notifications.Services.Implementations;
using GraNAS.Shared.Correlation;
using GraNAS.Shared.Infrastructure.Middleware;
using GraNAS.Shared.LoggingService;
using GraNAS.Shared.Messaging.DependencyInjection;
using GraNAS.Shared.Models.DTO;
using GraNAS.Shared.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;

namespace GraNAS.Notifications.API;

public class Program
{
    public static async Task Main(string[] args)
    {
        const string versionApi = "v1";
        const string apiTitle = "GraNAS.Notifications.API";
        const string corsPolicyName = "MyAllowSpecificOrigins";

        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseGraNasCentralLogging(apiTitle);

        builder.Services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(e => e.Value!.Errors.Count > 0)
                    .SelectMany(e => e.Value!.Errors.Select(er => er.ErrorMessage))
                    .ToList();
                return new BadRequestObjectResult(new ErrorResponse
                {
                    Error = "validation_error",
                    ErrorDescription = errors.FirstOrDefault() ?? "One or more validation errors occurred."
                });
            };
        });

        builder.Services.AddHttpContextAccessor();
        builder.AddPostgreSql<NotificationDbContext>();
        builder.Services.AddNotificationDal();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(corsPolicyName, policy =>
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        });

        var jwtSettings = builder.Configuration.GetSection("Jwt");
        var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = !builder.Environment.IsEnvironment("Test");
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(secretKey)
                };
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<Program>>();
                        logger.LogError(context.Exception, "JWT authentication failed");
                        return Task.CompletedTask;
                    },
                    OnMessageReceived = context =>
                    {
                        // Allow SignalR hub to get token from query string
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/hubs/notifications"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddHttpsRedirection(options =>
        {
            options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
            options.HttpsPort = 44344;
        });

        builder.Services.AddHttpClient<AuthServiceClient>(c =>
        {
            var baseUrl = builder.Configuration["AuthService:BaseUrl"]
                ?? throw new InvalidOperationException("AuthService:BaseUrl is not configured");
            c.BaseAddress = new Uri(baseUrl);
        }).AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

        builder.Services.AddGraNasCentralLoggingMvc();
        builder.Services.AddControllers();
        builder.Services.AddSignalR();

        builder.Services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("api", policy =>
            {
                policy.PermitLimit = 60;
                policy.Window = TimeSpan.FromMinutes(1);
                policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                policy.QueueLimit = 0;
            });

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(new ErrorResponse
                {
                    Error = "too_many_requests",
                    ErrorDescription = "Too many requests. Please try again later."
                });
            };
        });

        builder.Services.AddHealthChecks()
            .AddCheck("live", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddDbContextCheck<NotificationDbContext>("database", tags: ["ready"])
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["ready"]);

        builder.Services.AddHsts(options =>
        {
            options.Preload = true;
            options.IncludeSubDomains = true;
            options.MaxAge = TimeSpan.FromDays(365);
        });

        builder.Services.AddGraNasMessaging(builder.Configuration);

        builder.Services.AddHostedService<NotificationConsumerService>();
        builder.Services.AddHostedService<EmailDeliveryWorker>();
        builder.Services.AddHostedService<SignalRDeliveryWorker>();

        builder.Services.AddNotificationApplication(builder.Configuration);

        builder.Services.AddScoped<NotificationDeliveryService>();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerWithJwt(apiTitle, versionApi);

        builder.Services.AddCorrelationId();

        var app = builder.Build();

        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseCorrelationId();
        app.UseSerilogRequestLogging(opts =>
        {
            opts.GetLevel = (ctx, _, _) =>
                ctx.Request.Path.StartsWithSegments("/health")
                    ? LogEventLevel.Debug
                    : LogEventLevel.Information;
        });

        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
            app.UseHsts();
        }

        app.UseSecurityHeaders(policy =>
        {
            policy.AddDefaultSecurityHeaders();
            policy.AddFrameOptionsDeny();
            policy.AddXssProtectionBlock();
            policy.AddContentTypeOptionsNoSniff();
            policy.AddStrictTransportSecurityMaxAge((int)TimeSpan.FromDays(365).TotalSeconds);
        });

        app.UseCors(corsPolicyName);
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwaggerWithJwt(apiTitle, versionApi);
        }

        app.MapControllers().RequireRateLimiting("api");
        app.MapHub<NotificationsHub>("/hubs/notifications");

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = c => c.Tags.Contains("live")
        }).AllowAnonymous().DisableRateLimiting();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = c => c.Tags.Contains("ready")
        }).AllowAnonymous().DisableRateLimiting();

        try
        {
            using (var scope = app.Services.CreateScope())
            {
              var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
              await db.Database.MigrateAsync();
            }
            await app.RunAsync();
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
