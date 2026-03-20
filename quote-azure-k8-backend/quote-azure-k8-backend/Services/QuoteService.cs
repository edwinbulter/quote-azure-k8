using Microsoft.Extensions.Logging;
using quote_azure_k8_backend.Models;
using quote_azure_k8_backend.Data;

namespace quote_azure_k8_backend.Services
{
    public class QuoteService : IQuoteService
    {
        private readonly IQuoteRepository _quoteRepository;
        private readonly IUserActivityRepository _userActivityRepository;
        private readonly IZenQuotesService _zenQuotesService;
        private readonly ILogger<QuoteService> _logger;

        public QuoteService(
            IQuoteRepository quoteRepository,
            IUserActivityRepository userActivityRepository,
            IZenQuotesService zenQuotesService,
            ILogger<QuoteService> logger)
        {
            _quoteRepository = quoteRepository;
            _userActivityRepository = userActivityRepository;
            _zenQuotesService = zenQuotesService;
            _logger = logger;
        }

        public async Task<Quote?> GetQuoteAsync(string? username, HashSet<int> idsToExclude)
        {
            try
            {
                var allQuotes = await _quoteRepository.GetAllQuotesAsync();
                var availableQuotes = allQuotes
                    .Where(q => !idsToExclude.Contains(q.Id))
                    .ToList();

                if (!availableQuotes.Any())
                {
                    _logger.LogWarning("No available quotes found");
                    return null;
                }

                var random = new Random();
                var quote = availableQuotes[random.Next(availableQuotes.Count)];

                if (!string.IsNullOrEmpty(username))
                {
                    await RecordQuoteViewAsync(username, quote.Id);
                    await UpdateLastQuoteIdAsync(username, quote.Id);
                }

                return quote;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting quote");
                throw;
            }
        }

        public async Task<Quote?> GetQuoteByIdAsync(string? username, int quoteId)
        {
            try
            {
                var quote = await _quoteRepository.GetQuoteByIdAsync(quoteId);
                
                if (quote != null && !string.IsNullOrEmpty(username))
                {
                    await RecordQuoteViewAsync(username, quoteId);
                }

                return quote;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting quote by ID: {QuoteId}", quoteId);
                throw;
            }
        }

        public async Task<Quote?> LikeQuoteAsync(string username, int quoteId)
        {
            try
            {
                var quote = await _quoteRepository.GetQuoteByIdAsync(quoteId);
                if (quote == null)
                    return null;

                await _userActivityRepository.AddUserLikeAsync(username, quoteId);
                
                quote.LikeCount++;
                await _quoteRepository.UpdateQuoteAsync(quote);

                _logger.LogInformation("User {Username} liked quote {QuoteId}", username, quoteId);
                return quote;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error liking quote for user: {Username}, quote: {QuoteId}", username, quoteId);
                throw;
            }
        }

        public async Task UnlikeQuoteAsync(string username, int quoteId)
        {
            try
            {
                await _userActivityRepository.RemoveUserLikeAsync(username, quoteId);
                
                var quote = await _quoteRepository.GetQuoteByIdAsync(quoteId);
                if (quote != null && quote.LikeCount > 0)
                {
                    quote.LikeCount--;
                    await _quoteRepository.UpdateQuoteAsync(quote);
                }

                _logger.LogInformation("User {Username} unliked quote {QuoteId}", username, quoteId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unliking quote for user: {Username}, quote: {QuoteId}", username, quoteId);
                throw;
            }
        }

        public async Task<List<Quote>> GetLikedQuotesByUserAsync(string username)
        {
            try
            {
                var likedQuoteIds = await _userActivityRepository.GetUserLikedQuoteIdsAsync(username);
                var likedQuotes = new List<Quote>();

                foreach (var quoteId in likedQuoteIds)
                {
                    var quote = await _quoteRepository.GetQuoteByIdAsync(quoteId);
                    if (quote != null)
                        likedQuotes.Add(quote);
                }

                return likedQuotes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting liked quotes for user: {Username}", username);
                throw;
            }
        }

        public async Task<List<Quote>> GetViewedQuotesAsync(string username)
        {
            try
            {
                // This is a simplified implementation
                // In a real scenario, you would track viewed quotes in a separate table
                var allQuotes = await _quoteRepository.GetAllQuotesAsync();
                return allQuotes.Take(10).ToList(); // Return last 10 quotes as viewed
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting viewed quotes for user: {Username}", username);
                throw;
            }
        }

        public async Task ReorderLikedQuoteAsync(string username, int quoteId, int newOrder)
        {
            try
            {
                await _userActivityRepository.UpdateUserLikeOrderAsync(username, quoteId, newOrder);
                _logger.LogInformation("User {Username} reordered quote {QuoteId} to position {NewOrder}", username, quoteId, newOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering liked quote for user: {Username}, quote: {QuoteId}", username, quoteId);
                throw;
            }
        }

        public async Task<UserProgress?> GetUserProgressAsync(string username)
        {
            try
            {
                return await _userActivityRepository.GetUserProgressAsync(username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user progress for user: {Username}", username);
                throw;
            }
        }

        private async Task RecordQuoteViewAsync(string username, int quoteId)
        {
            // This is a simplified implementation
            // In a real scenario, you would store this in a userviews table
            _logger.LogDebug("Recording quote view for user: {Username}, quote: {QuoteId}", username, quoteId);
        }

        private async Task UpdateLastQuoteIdAsync(string username, int quoteId)
        {
            await _userActivityRepository.UpdateLastQuoteIdAsync(username, quoteId);
        }
    }
}
