using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Azure.Data.Tables;
using System.Text;
using quote_azure_k8_backend.Services;
using quote_azure_k8_backend.Data;
using quote_azure_k8_backend.Models;
using quote_azure_k8_backend.Middleware;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.WriteIndented = false;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register HttpClient
builder.Services.AddHttpClient();

// Register Table Storage client
builder.Services.AddSingleton(sp => {
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration["TableStorageConnectionString"];
    return new TableServiceClient(connectionString);
});

// Register repositories
builder.Services.AddSingleton<IQuoteRepository, QuoteRepository>();
builder.Services.AddSingleton<IUserActivityRepository, UserActivityRepository>();
builder.Services.AddSingleton<IUserRoleRepository, UserRoleRepository>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();

// Register services
builder.Services.AddSingleton<IQuoteService, QuoteService>();
builder.Services.AddSingleton<IZenQuotesService, ZenQuotesService>();
builder.Services.AddSingleton<IQuoteManagementService, QuoteManagementService>();

// Register JWT authentication services
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<JwtAuthenticationMiddleware>();

// Register password hasher for JWT authentication
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// Register admin services
builder.Services.AddSingleton<IAdminService, AdminService>();

// Register admin user seeder
builder.Services.AddSingleton<AdminUserSeeder>();

// Add JWT authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Add logging
builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed admin users on startup
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<AdminUserSeeder>();
    await seeder.SeedAdminUsersAsync();
}

app.Run();