using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Otrade.Application.Common.Interfaces;
using Otrade.Application.Common.Locks;
using Otrade.Application.Services;
using Otrade.Application.Services.Security;
using Otrade.BackgroundJobs.Jobs;
using Otrade.BackgroundJobs.Scheduler;
using Otrade.Persistence.Context;
using Otrade.web.BackgroundServices;
using Otrade.web.Security;
using Otrade.Web.Services;
using System.Text;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
var jwtSettings = builder.Configuration.GetSection("JwtSettings");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["Key"]!))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];

                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hangfire"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var userIdValue =
                    context.Principal?
                        .FindFirst("userId")?
                        .Value;

                var tokenVersionValue =
                    context.Principal?
                        .FindFirst("tokenVersion")?
                        .Value;

                if (
                    !long.TryParse(
                        userIdValue,
                        out var userId) ||
                    !int.TryParse(
                        tokenVersionValue,
                        out var tokenVersion)
                )
                {
                    context.Fail(
                        "Invalid token claims.");

                    return;
                }

                var dbContext =
                    context.HttpContext
                        .RequestServices
                        .GetRequiredService<
                            OtradeDbContext>();

                var userSecurity =
                    await dbContext.Users
                        .AsNoTracking()
                        .Where(x =>
                            x.UserId == userId)
                        .Select(x => new
                        {
                            x.AuthTokenVersion,
                            x.MustChangePassword
                        })
                        .FirstOrDefaultAsync();

                if (
                    userSecurity == null ||
                    userSecurity.AuthTokenVersion !=
                    tokenVersion
                )
                {
                    context.Fail(
                        "Token has been revoked.");

                    return;
                }

                /*
                 * Refresh the password-change requirement from the database.
                 * This lets the same valid recovery session continue immediately
                 * after the required password change is completed.
                 */
                if (
                    context.Principal?.Identity
                    is System.Security.Claims.ClaimsIdentity
                        identity
                )
                {
                    var oldClaim =
                        identity.FindFirst(
                            "mustChangePassword");

                    if (oldClaim != null)
                    {
                        identity.RemoveClaim(
                            oldClaim);
                    }

                    identity.AddClaim(
                        new System.Security.Claims.Claim(
                            "mustChangePassword",
                            userSecurity
                                .MustChangePassword
                                .ToString()));
                }
            }

        };
    });

builder.Services.AddAuthorization();
builder.Services.AddDbContext<OtradeDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHangfire(config =>
{
    config.UseSqlServerStorage(
        builder.Configuration.GetConnectionString("Hangfire"));
});

builder.Services.AddHangfireServer();
builder.Services.AddScoped<IFileStorageService,FileStorageService>();
builder.Services.AddScoped<KycService>(); 
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<ContractService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PreRegistrationService>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<PaymentTransactionGuardService>();
builder.Services.AddScoped<JobLockservice>();
builder.Services.AddScoped<MainInvestBonusService>();
builder.Services.AddScoped<InvestmentCapacityService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<RankService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<ReferralProfitService>();
builder.Services.AddScoped<RankJob>();
builder.Services.AddScoped<ContractExpiryJob>();
builder.Services.AddScoped<InvestmentProfitJob>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<SystemSettingService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<EncryptionService>();
builder.Services.AddSingleton<TotpSecretProtector>();
builder.Services.AddScoped<TwoFactorAuthenticationService>();
builder.Services.AddScoped<TwoFactorRecoveryService>();
builder.Services.AddSingleton<INotificationQueue, NotificationQueue>();
builder.Services.AddHostedService<NotificationBackgroundWorker>();
builder.Services.AddScoped<AdminPermissionService>();
builder.Services.AddScoped<InvestmentWaitListService>();
builder.Services.AddScoped<BonusCodeService>();
builder.Services.AddScoped<WalletBalanceSnapshotService>();
builder.Services.AddScoped<WalletBalanceSnapshotJob>();
var app = builder.Build();

// Configure the HTTP request pipeline.


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.Use(
    async (
        context,
        next) =>
    {
        var isAuthenticated =
            context.User.Identity?
                .IsAuthenticated ==
            true;

        var mustChangePassword =
            bool.TryParse(
                context.User
                    .FindFirst(
                        "mustChangePassword")?
                    .Value,
                out var required) &&
            required;

        if (
            isAuthenticated &&
            mustChangePassword
        )
        {
            var path =
                context.Request.Path;

            var allowed =
                path.StartsWithSegments(
                    "/profile") ||
                path.StartsWithSegments(
                    "/api/profile") ||
                path.StartsWithSegments(
                    "/error");

            if (!allowed)
            {
                if (
                    path.StartsWithSegments(
                        "/api")
                )
                {
                    context.Response.StatusCode =
                        StatusCodes
                            .Status428PreconditionRequired;

                    context.Response.ContentType =
                        "application/json";

                    await context.Response
                        .WriteAsJsonAsync(
                            new
                            {
                                success =
                                    false,

                                message =
                                    "You must change your password before continuing.",

                                code =
                                    "PASSWORD_CHANGE_REQUIRED"
                            });

                    return;
                }

                context.Response.Redirect(
                    "/profile?forcePassword=1");

                return;
            }
        }

        await next();
    });
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[]
    {
        new HangfireDashboardAuthorizationFilter(
            app.Services.GetRequiredService<IDataProtectionProvider>())
    }
});
app.MapStaticAssets();
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    var request = context.HttpContext.Request;

    if (response.StatusCode == 404)
    {
        if (request.Path.StartsWithSegments("/api"))
        {
            response.Redirect("/error/api-404");
        }
        else
        {
            response.Redirect("/error/404");
        }
    }
});
JobScheduler.Register(app.Services);
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}")
    .WithStaticAssets();


app.Run();
//app.Run("http://0.0.0.0:5050");
