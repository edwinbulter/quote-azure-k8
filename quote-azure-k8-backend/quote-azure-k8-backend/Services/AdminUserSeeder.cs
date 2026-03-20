using Microsoft.Extensions.Logging;
using quote_azure_k8_backend.Models;
using quote_azure_k8_backend.Data;
using Microsoft.AspNetCore.Identity;

namespace quote_azure_k8_backend.Services
{
    public class AdminUserSeeder
    {
        private readonly IUserRepository _userRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ILogger<AdminUserSeeder> _logger;

        public AdminUserSeeder(
            IUserRepository userRepository,
            IUserRoleRepository userRoleRepository,
            IPasswordHasher<User> passwordHasher,
            ILogger<AdminUserSeeder> logger)
        {
            _userRepository = userRepository;
            _userRoleRepository = userRoleRepository;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        public async Task SeedAdminUsersAsync()
        {
            try
            {
                _logger.LogInformation("Starting admin user seeding process");

                // Wait a bit for tables to be created asynchronously
                await Task.Delay(2000);

                // Check if admin user already exists with retry logic
                User? existingAdmin = null;
                var maxRetries = 3;
                var retryCount = 0;
                
                while (retryCount < maxRetries)
                {
                    try
                    {
                        existingAdmin = await _userRepository.GetByUsernameAsync("admin");
                        break;
                    }
                    catch (Azure.RequestFailedException ex) when (ex.Status == 404)
                    {
                        retryCount++;
                        _logger.LogWarning("Tables not ready yet, retrying in 2 seconds... (Attempt {RetryCount}/{MaxRetries})", retryCount, maxRetries);
                        await Task.Delay(2000);
                    }
                }

                if (existingAdmin != null)
                {
                    _logger.LogInformation("Admin user already exists, updating ID and checking admin role assignment...");
                    
                    // Update the admin user to have the correct ID that matches JWT tokens
                    if (existingAdmin.Id != "98d9a7e2-2657-4c37-b784-2eee61ddb3b8")
                    {
                        existingAdmin.Id = "98d9a7e2-2657-4c37-b784-2eee61ddb3b8";
                        await _userRepository.UpdateAsync(existingAdmin);
                        _logger.LogInformation("Updated admin user ID to match JWT token");
                    }
                    
                    // Check if admin role is assigned
                    var existingRole = await _userRoleRepository.GetUserRoleAsync("admin");
                    if (existingRole != null && existingRole.Role == "ADMIN")
                    {
                        _logger.LogInformation("Admin role already assigned, seeding complete");
                        return;
                    }
                    else
                    {
                        _logger.LogInformation("Admin role not found or incorrect, assigning ADMIN role...");
                        
                        // Assign admin role
                        var adminRoleAssignment = new UserRole
                        {
                            Username = "admin",
                            Role = "ADMIN",
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "System"
                        };

                        await _userRoleRepository.CreateUserRoleAsync(adminRoleAssignment);
                        _logger.LogInformation("Admin role assigned to existing admin user");
                        return;
                    }
                }

                // Create admin user
                var adminUser = new User
                {
                    Id = "98d9a7e2-2657-4c37-b784-2eee61ddb3b8", // Fixed ID to match JWT tokens
                    Username = "admin",
                    Email = "admin@quote-backend.local",
                    PasswordHash = _passwordHasher.HashPassword(null!, "Admin123!"),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                var createdUser = await _userRepository.CreateAsync(adminUser);
                _logger.LogInformation("Admin user created: {Username}", createdUser.Username);

                // Assign admin role
                var adminRole = new UserRole
                {
                    Username = createdUser.Username,
                    Role = "ADMIN",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                await _userRoleRepository.CreateUserRoleAsync(adminRole);
                _logger.LogInformation("Admin role assigned to user: {Username}", createdUser.Username);

                // Create test user
                var existingTestUser = await _userRepository.GetByUsernameAsync("user-1");
                if (existingTestUser != null)
                {
                    _logger.LogInformation("Test user already exists, skipping creation");
                    return;
                }

                var testUser = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = "user-1",
                    Email = "user-1@outlook.com",
                    PasswordHash = _passwordHasher.HashPassword(null!, "Hello-user-1"),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                var createdTestUser = await _userRepository.CreateAsync(testUser);
                _logger.LogInformation("Test user created: {Username}", createdTestUser.Username);

                // Assign user role
                var userRole = new UserRole
                {
                    Username = createdTestUser.Username,
                    Role = "USER",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                await _userRoleRepository.CreateUserRoleAsync(userRole);
                _logger.LogInformation("User role assigned to user: {Username}", createdTestUser.Username);

                _logger.LogInformation("Admin user seeding completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during admin user seeding");
                throw;
            }
        }
    }
}
