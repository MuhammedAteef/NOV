using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;
using SystemManagementSystem.Services;
using SystemManagementSystem.DTO;
using DotNetEnv;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<SalesManagementSystemContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// Add Identity & Cookie Authentication
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedEmail = true;

    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 9;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<SalesManagementSystemContext>()
.AddDefaultTokenProviders();

// Configure Email Settings
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddScoped<IEmailService, EmailService>();


// Configure Razor Pages Authorization Rules
builder.Services.AddRazorPages(options =>
{
    // Public Pages
    options.Conventions.AllowAnonymousToPage("/Index");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/Register");

    // Dashboard requires authentication
    options.Conventions.AuthorizePage("/Dashboard");
    options.Conventions.AllowAnonymousToPage("/Profile/ConfirmEmail");
    //options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");

    // Admin Only
    options.Conventions.AuthorizeFolder("/Categories", "AdminOnly");
    options.Conventions.AuthorizeFolder("/Suppliers", "AdminOnly");
    options.Conventions.AuthorizeFolder("/PurchaseInvoices", "AdminOnly");
    options.Conventions.AuthorizeFolder("/PurchaseInvoiceItems", "AdminOnly");
    options.Conventions.AuthorizeFolder("/StockHistory", "AdminOnly");
    options.Conventions.AuthorizeFolder("/InvoiceHistory", "AdminOnly");

    // Authenticated Users (Admin + User)
    options.Conventions.AuthorizeFolder("/Products");
    options.Conventions.AuthorizeFolder("/Customers");
    options.Conventions.AuthorizeFolder("/SalesInvoices");
    options.Conventions.AuthorizeFolder("/SalesInvoiceItems");
    options.Conventions.AuthorizeFolder("/Profile");

});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));
});

// Register Repositories and Unit of Work
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IPurchaseInvoiceRepository, PurchaseInvoiceRepository>();
builder.Services.AddScoped<ISalesInvoiceRepository, SalesInvoiceRepository>();
builder.Services.AddScoped<IAuthRepo, AuthRepo>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Register Domain Services
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IInvoiceWorkflowService, InvoiceWorkflowService>();
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

// Apply migrations and seed roles plus an optional configured bootstrap administrator.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<SalesManagementSystemContext>();
        await context.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        string[] roles = { "Admin", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not create the '{role}' role: {string.Join("; ", roleResult.Errors.Select(error => error.Code))}");
                }
            }
        }

        var adminEmail = builder.Configuration["SeedAdmin:Email"];
        var adminPassword = builder.Configuration["SeedAdmin:Password"];
        var adminUserName = builder.Configuration["SeedAdmin:UserName"];
        if (string.IsNullOrWhiteSpace(adminEmail) != string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "Configure both SeedAdmin:Email and SeedAdmin:Password to provision the bootstrap administrator.");
        }

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser is null)
            {
                adminUser = new IdentityUser
                {
                    UserName = adminUserName,
                    Email = adminEmail,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not create the configured bootstrap administrator: {string.Join("; ", result.Errors.Select(error => error.Code))}");
                }
            }

            if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                var roleResult = await userManager.AddToRoleAsync(adminUser, "Admin");
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not assign the configured bootstrap administrator to the Admin role: {string.Join("; ", roleResult.Errors.Select(error => error.Code))}");
                }
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding Identity roles and users.");
        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
