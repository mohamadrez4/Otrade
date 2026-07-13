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
builder.Services.AddSingleton<INotificationQueue, NotificationQueue>();
builder.Services.AddHostedService<NotificationBackgroundWorker>();
builder.Services.AddScoped<AdminPermissionService>();
builder.Services.AddScoped<InvestmentWaitListService>();
builder.Services.AddScoped<BonusCodeService>();
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
