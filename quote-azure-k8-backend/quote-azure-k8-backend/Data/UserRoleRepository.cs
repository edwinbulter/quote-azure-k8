using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using quote_azure_k8_backend.Models;

namespace quote_azure_k8_backend.Data
{
    public class UserRoleRepository : IUserRoleRepository
    {
        private readonly TableClient _tableClient;
        private readonly ILogger<UserRoleRepository> _logger;

        public UserRoleRepository(TableServiceClient tableServiceClient, ILogger<UserRoleRepository> logger)
        {
            _tableClient = tableServiceClient.GetTableClient("userroles");
            _tableClient.CreateIfNotExists();
            _logger = logger;
        }

        public async Task<UserRole?> GetUserRoleAsync(string username)
        {
            try
            {
                var response = await _tableClient.GetEntityAsync<TableEntity>("userroles", username);
                var entity = response.Value;
                
                return new UserRole
                {
                    Username = entity["Username"].ToString(),
                    Role = entity["Role"].ToString(),
                    CreatedAt = (DateTime)entity["CreatedAt"],
                    UpdatedAt = entity["UpdatedAt"] as DateTime?,
                    CreatedBy = entity["CreatedBy"].ToString(),
                    UpdatedBy = entity["UpdatedBy"]?.ToString()
                };
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user role for user: {Username}", username);
                throw;
            }
        }

        public async Task<List<UserRole>> GetAllUserRolesAsync()
        {
            try
            {
                var userRoles = new List<UserRole>();
                var query = _tableClient.QueryAsync<TableEntity>();
                
                await foreach (var entity in query)
                {
                    var userRole = new UserRole
                    {
                        Username = entity["Username"].ToString(),
                        Role = entity["Role"].ToString(),
                        CreatedAt = (DateTime)entity["CreatedAt"],
                        UpdatedAt = entity["UpdatedAt"] as DateTime?,
                        CreatedBy = entity["CreatedBy"].ToString(),
                        UpdatedBy = entity["UpdatedBy"]?.ToString()
                    };
                    userRoles.Add(userRole);
                }
                return userRoles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all user roles");
                throw;
            }
        }

        public async Task<UserRole> CreateUserRoleAsync(UserRole userRole)
        {
            var entity = new TableEntity("userroles", userRole.Username)
            {
                ["Username"] = userRole.Username,
                ["Role"] = userRole.Role,
                ["CreatedAt"] = userRole.CreatedAt,
                ["UpdatedAt"] = userRole.UpdatedAt,
                ["CreatedBy"] = userRole.CreatedBy,
                ["UpdatedBy"] = userRole.UpdatedBy
            };

            try
            {
                await _tableClient.AddEntityAsync(entity);
                _logger.LogInformation("User role created successfully for user: {Username}", userRole.Username);
                return userRole;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Error creating user role for user: {Username}", userRole.Username);
                throw;
            }
        }

        public async Task<UserRole> UpdateUserRoleAsync(UserRole userRole)
        {
            var entity = new TableEntity("userroles", userRole.Username)
            {
                ["Username"] = userRole.Username,
                ["Role"] = userRole.Role,
                ["CreatedAt"] = userRole.CreatedAt,
                ["UpdatedAt"] = DateTime.UtcNow,
                ["CreatedBy"] = userRole.CreatedBy,
                ["UpdatedBy"] = userRole.UpdatedBy
            };

            try
            {
                await _tableClient.UpdateEntityAsync(entity, ETag.All);
                userRole.UpdatedAt = DateTime.UtcNow;
                _logger.LogInformation("User role updated successfully for user: {Username}", userRole.Username);
                return userRole;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Error updating user role for user: {Username}", userRole.Username);
                throw;
            }
        }

        public async Task<bool> DeleteUserRoleAsync(string username)
        {
            try
            {
                await _tableClient.DeleteEntityAsync("userroles", username);
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user role for user: {Username}", username);
                return false;
            }
        }

        public async Task<bool> UserHasRoleAsync(string username, string role)
        {
            try
            {
                var userRole = await GetUserRoleAsync(username);
                return userRole != null && userRole.Role.Equals(role, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user has role for user: {Username}, role: {Role}", username, role);
                return false;
            }
        }
    }
}
