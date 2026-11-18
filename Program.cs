using DeanFlow.Data;
using DeanFlow.Data.Entities;
using DeanFlow.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("DeanFlow");
if (builder.Environment.IsDevelopment())
{
    var localKeyDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "development-keys");
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(localKeyDirectory));
}
else if (OperatingSystem.IsWindows())
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")))
        .ProtectKeysWithDpapi();
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection қолданба баптауында көрсетілмеген.");

builder.Services.AddDbContext<DeanFlowDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<DocumentAccessService>();
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddScoped<CookieClaimsValidationEvents>();
builder.Services.AddScoped<PasswordHasher<AppUser>>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole(AppRoles.Administrator));
    options.AddPolicy("DocumentManagers", p => p.RequireRole(AppRoles.Administrator, AppRoles.DeanerySpecialist));
    options.AddPolicy("ManagersAndExecutors", p => p.RequireRole(AppRoles.Administrator, AppRoles.DeanerySpecialist, AppRoles.Executor));
    options.AddPolicy("Approvers", p => p.RequireRole(AppRoles.Administrator, AppRoles.Management));
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "DeanFlow.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.EventsType = typeof(CookieClaimsValidationEvents);
});

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseStatusCodePagesWithReExecute("/Home/StatusCodeError", "?code={0}");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
