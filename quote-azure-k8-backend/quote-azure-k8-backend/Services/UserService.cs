using Microsoft.Extensions.Logging;
using quote_azure_k8_backend.Models;
using quote_azure_k8_backend.Models.Auth;
using quote_azure_k8_backend.Data;
using Microsoft.AspNetCore.Identity;

namespace quote_azure_k8_backend.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IJwtService _jwtService;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ILogger<UserService> _logger;

        public UserService(
            IUserRepository userRepository,
            IUserRoleRepository userRoleRepository,
            IJwtService jwtService,
            IPasswordHasher<User> passwordHasher,
            ILogger<UserService> logger)
        {
            _userRepository = userRepository;
            _userRoleRepository = userRoleRepository;
            _jwtService = jwtService;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        public async Task<User> RegisterAsync(RegisterRequest request)
        {
            try
            {
                // Check if user already exists
                if (await _userRepository.EmailExistsAsync(request.Email))
                    throw new ArgumentException("Email already exists");

                if (await _userRepository.UsernameExistsAsync(request.Username))
                    throw new ArgumentException("Username already exists");

                // Create new user
                var user = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = request.Email,
                    Username = request.Username,
                    PasswordHash = _passwordHasher.HashPassword(null!, request.Password),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                var createdUser = await _userRepository.CreateAsync(user);

                // Assign USER role by default
                var userRole = new UserRole
                {
                    Username = createdUser.Username,
                    Role = "USER",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                await _userRoleRepository.CreateUserRoleAsync(userRole);

                _logger.LogInformation("User registered successfully: {Username}", createdUser.Username);
                return createdUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user");
                throw;
            }
        }

        public async Task<string> LoginAsync(LoginRequest request)
        {
            try
            {
                User? user = null;

                // Try to find user by email first, then by username
                if (request.LoginIdentifier.Contains("@"))
                {
                    user = await _userRepository.GetByEmailAsync(request.LoginIdentifier);
                }
                else
                {
                    user = await _userRepository.GetByUsernameAsync(request.LoginIdentifier);
                }

                if (user == null)
                    throw new UnauthorizedAccessException("User not found");

                // Verify password
                var verificationResult = _passwordHasher.VerifyHashedPassword(null!, user.PasswordHash, request.Password);
                if (verificationResult != PasswordVerificationResult.Success)
                    throw new UnauthorizedAccessException("Invalid password");

                if (!user.IsActive)
                    throw new UnauthorizedAccessException("User account is inactive");

                // Generate JWT token
                var token = _jwtService.GenerateToken(user);

                _logger.LogInformation("User logged in successfully: {Username}", user.Username);
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                throw;
            }
        }

        public async Task<bool> ChangePasswordAsync(string userId, ChangePasswordRequest request)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return false;

                // Verify current password
                var verificationResult = _passwordHasher.VerifyHashedPassword(null!, user.PasswordHash, request.CurrentPassword);
                if (verificationResult != PasswordVerificationResult.Success)
                    throw new UnauthorizedAccessException("Current password is incorrect");

                // Update password
                user.PasswordHash = _passwordHasher.HashPassword(null!, request.NewPassword);
                user.UpdatedAt = DateTime.UtcNow;

                await _userRepository.UpdateAsync(user);

                _logger.LogInformation("Password changed successfully for user: {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                throw;
            }
        }

        public async Task<bool> UpdateUserRoleAsync(string adminId, UpdateRoleRequest request)
        {
            try
            {
                // Check if admin is actually an admin
                var admin = await _userRepository.GetByIdAsync(adminId);
                if (admin == null || !await IsAdminAsync(adminId))
                    throw new UnauthorizedAccessException("Only admins can update user roles");

                var targetUser = await _userRepository.GetByUsernameAsync(request.UserId);
                if (targetUser == null)
                    return false;

                var existingRole = await _userRoleRepository.GetUserRoleAsync(targetUser.Username);
                if (existingRole != null)
                {
                    existingRole.Role = request.NewRole;
                    existingRole.UpdatedAt = DateTime.UtcNow;
                    existingRole.UpdatedBy = admin.Username;
                    await _userRoleRepository.UpdateUserRoleAsync(existingRole);
                }
                else
                {
                    var newRole = new UserRole
                    {
                        Username = targetUser.Username,
                        Role = request.NewRole,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = admin.Username
                    };
                    await _userRoleRepository.CreateUserRoleAsync(newRole);
                }

                _logger.LogInformation("User role updated: {Username} -> {Role}", targetUser.Username, request.NewRole);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user role");
                throw;
            }
        }

        public async Task<bool> RemoveUserRoleAsync(string adminId, UpdateRoleRequest request)
        {
            try
            {
                // Check if admin is actually an admin
                if (!await IsAdminAsync(adminId))
                    throw new UnauthorizedAccessException("Only admins can remove user roles");

                var targetUser = await _userRepository.GetByUsernameAsync(request.UserId);
                if (targetUser == null)
                    return false;

                await _userRoleRepository.DeleteUserRoleAsync(targetUser.Username);

                _logger.LogInformation("User role removed: {Username}", targetUser.Username);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user role");
                throw;
            }
        }

        public async Task<User?> GetUserByIdAsync(string id)
        {
            return await _userRepository.GetByIdAsync(id);
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            return await _userRepository.GetByUsernameAsync(username);
        }

        public async Task<IEnumerable<Models.Admin.AdminUserInfo>> GetAllUsersAsync(string adminId)
        {
            try
            {
                if (!await IsAdminAsync(adminId))
                    throw new UnauthorizedAccessException("Only admins can list all users");

                var users = await _userRepository.GetAllAsync();
                var userInfos = new List<Models.Admin.AdminUserInfo>();

                foreach (var user in users)
                {
                    var userRole = await _userRoleRepository.GetUserRoleAsync(user.Username);
                    userInfos.Add(new Models.Admin.AdminUserInfo
                    {
                        Username = user.Username,
                        Email = user.Email,
                        Roles = userRole != null ? new[] { userRole.Role } : new string[0],
                        Enabled = user.IsActive,
                        UserStatus = user.IsActive ? "Active" : "Inactive",
                        UserCreateDate = user.CreatedAt.ToString("yyyy-MM-dd"),
                        UserLastModifiedDate = user.UpdatedAt.ToString("yyyy-MM-dd")
                    });
                }

                return userInfos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users");
                throw;
            }
        }

        public async Task<bool> IsUserInRoleAsync(string userId, string role)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return false;

                return await _userRoleRepository.UserHasRoleAsync(user.Username, role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user role");
                return false;
            }
        }

        public async Task<bool> IsAdminAsync(string userId)
        {
            return await IsUserInRoleAsync(userId, "ADMIN");
        }

        public async Task<bool> UnregisterAsync(string userId, string password)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return false;

                // Verify password
                var verificationResult = _passwordHasher.VerifyHashedPassword(null!, user.PasswordHash, password);
                if (verificationResult != PasswordVerificationResult.Success)
                    throw new UnauthorizedAccessException("Invalid password");

                // Delete all user data
                await _userRepository.DeleteAsync(userId);
                // Note: In a real implementation, you would also delete related data (likes, progress, etc.)

                _logger.LogInformation("User unregistered successfully: {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unregistering user");
                throw;
            }
        }

        public async Task<bool> DeleteUserAsync(string userId)
        {
            try
            {
                return await _userRepository.DeleteAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                return false;
            }
        }
    }
}
