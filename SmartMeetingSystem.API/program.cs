using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartMeetingSystem.Domain.Entities;
using SmartMeetingSystem.Infrastructure;
using SmartMeetingSystem.Application.IService;
using SmartMeetingSystem.Application.Service;
using SmartMeetingSystem.Application.IRepository;
using SmartMeetingSystem.Infrastructure.Repository;
using Microsoft.AspNetCore.Routing;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidAudience = config["Jwt:Audience"],
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IMomRepository, MomRepository>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IMomService, MomService>();

builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(SmartMeetingSystem.API.Controllers.RoomsController).Assembly);

Console.WriteLine("✅ Controllers have been registered from assembly: " +
    typeof(SmartMeetingSystem.API.Controllers.RoomsController).Assembly.FullName);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SmartMeetingSystem API", Version = "v1" });
    var securitySchema = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter 'Bearer {token}'",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", securitySchema);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securitySchema, new[] { "Bearer" } }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

// ✅ Print endpoints AFTER mapping controllers
var endpointDataSource = app.Services.GetRequiredService<EndpointDataSource>();
Console.WriteLine("=== Registered Endpoints ===");
foreach (var endpoint in endpointDataSource.Endpoints)
{
    Console.WriteLine(endpoint.DisplayName);
}
Console.WriteLine("===========================");

using (var scope = app.Services.CreateScope())
{
    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    string[] roles = ["Admin", "User"];
    foreach (var r in roles)
        if (!await roleMgr.RoleExistsAsync(r))
            await roleMgr.CreateAsync(new IdentityRole(r));

    var adminEmail = "admin@smartmeetings.local";
    var admin = await userMgr.FindByEmailAsync(adminEmail);
    if (admin == null)
    {
        admin = new AppUser
        {
            UserName = "admin",
            Email = adminEmail,
            EmailConfirmed = true,
            FullName = "System Admin"
        };
        await userMgr.CreateAsync(admin, "Admin#12345");
        await userMgr.AddToRoleAsync(admin, "Admin");
    }
}

app.Run();
