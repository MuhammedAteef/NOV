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
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

// Seed Roles and Default Users
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
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
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Seed Default Admin User
        var adminEmail = "admin1@system.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            var result = await userManager.CreateAsync(adminUser, "Admin1@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        var standardEmail = "admin2@system.com";
        var standardAdmin = await userManager.FindByEmailAsync(standardEmail);
        if (standardAdmin == null)
        {
            standardAdmin = new IdentityUser { UserName = standardEmail, Email = standardEmail, EmailConfirmed = true };
            var result = await userManager.CreateAsync(standardAdmin, "Admin2@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(standardAdmin, "Admin");
            }
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding Identity roles and users.");
    }
}

// Configure the HTTP request pipeline.
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
