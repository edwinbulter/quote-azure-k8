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

                // Check if admin user already exists
                var existingAdmin = await _userRepository.GetByUsernameAsync("admin");
                if (existingAdmin != null)
                {
                    _logger.LogInformation("Admin user already exists, skipping seeding");
                    return;
                }

                // Create admin user
                var adminUser = new User
                {
                    Id = Guid.NewGuid().ToString(),
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
