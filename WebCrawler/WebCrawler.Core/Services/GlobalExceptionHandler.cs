using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using WebCrawler.Core.Interfaces;

namespace WebCrawler.Core.Services
{
    /// <summary>
    /// Global exception handler for the application
    /// </summary>
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles exceptions with logging and formatted console output
        /// </summary>
        public void Handle(Exception exception)
        {
            if (exception == null)
                return;

            _logger.LogError(exception, "An exception occurred in the application");
            Console.WriteLine();

            switch (exception)
            {
                case HttpRequestException httpEx:
                    HandleHttpException(httpEx);
                    break;

                case OperationCanceledException opEx:
                    HandleTimeoutException(opEx);
                    break;

                case UriFormatException uriEx:
                    HandleUriException(uriEx);
                    break;

                case ArgumentException argEx:
                    HandleArgumentException(argEx);
                    break;

                case InvalidOperationException opEx:
                    HandleInvalidOperationException(opEx);
                    break;

                default:
                    HandleGenericException(exception);
                    break;
            }
        }

        /// <summary>
        /// Executes an async action with exception handling
        /// </summary>
        public async Task ExecuteAsync(Func<Task> action, string operationName = "Operation")
        {
            try
            {
                if (action == null)
                    throw new ArgumentNullException(nameof(action));

                _logger.LogInformation("Starting operation: {OperationName}", operationName);
                await action();
                _logger.LogInformation("Operation completed successfully: {OperationName}", operationName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Operation failed: {OperationName}", operationName);
                Handle(ex);
                throw;
            }
        }

        /// <summary>
        /// Executes an async action with exception handling and result return
        /// </summary>
        public async Task<T?> ExecuteAsync<T>(Func<Task<T>> action, string operationName = "Operation")
        {
            try
            {
                if (action == null)
                    throw new ArgumentNullException(nameof(action));

                _logger.LogInformation("Starting operation: {OperationName}", operationName);
                var result = await action();
                _logger.LogInformation("Operation completed successfully: {OperationName}", operationName);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Operation failed: {OperationName}", operationName);
                Handle(ex);
                throw;
            }
        }

        private void HandleHttpException(HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request exception occurred");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] NETWORK ERROR");
            Console.ResetColor();

            if (ex.InnerException is TimeoutException)
            {
                Console.WriteLine("   -> Request timed out. The server took too long to respond.");
            }
            else if (ex.StatusCode.HasValue)
            {
                Console.WriteLine($"   -> HTTP {(int)ex.StatusCode}: {ex.StatusCode}");
                Console.WriteLine($"      {GetHttpStatusDescription((int)ex.StatusCode)}");
            }
            else
            {
                Console.WriteLine($"   -> Connection failed: {ex.Message}");
            }
        }

        private void HandleTimeoutException(OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Operation timeout exception occurred");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[TIMEOUT] TIMEOUT ERROR");
            Console.ResetColor();
            Console.WriteLine("   -> Request timeout. The operation took too long to complete.");
            Console.WriteLine("   -> Check your internet connection or increase the timeout setting.");
        }

        private void HandleUriException(UriFormatException ex)
        {
            _logger.LogError(ex, "URI format exception occurred");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] INVALID URL FORMAT");
            Console.ResetColor();
            Console.WriteLine($"   -> The URL provided is not in a valid format.");
            Console.WriteLine($"   -> Please ensure the URL starts with 'http://' or 'https://'");
        }

        private void HandleArgumentException(ArgumentException ex)
        {
            _logger.LogError(ex, "Argument exception occurred for parameter: {ParamName}", ex.ParamName);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] INVALID ARGUMENT");
            Console.ResetColor();
            Console.WriteLine($"   -> {ex.Message}");
            Console.WriteLine($"   -> Parameter: {ex.ParamName}");
        }

        private void HandleInvalidOperationException(InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation exception occurred");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[WARNING] OPERATION ERROR");
            Console.ResetColor();
            Console.WriteLine($"   -> {ex.Message}");
        }

        private void HandleGenericException(Exception ex)
        {
            _logger.LogCritical(ex, "Unexpected exception occurred");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[CRITICAL] UNEXPECTED ERROR");
            Console.ResetColor();

            if (!string.IsNullOrEmpty(ex.Message))
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"   -> {ex.Message}");
                Console.ResetColor();
            }
        }

        private static string GetHttpStatusDescription(int statusCode)
        {
            return statusCode switch
            {
                400 => "Bad Request - The server cannot process the request due to client error.",
                401 => "Unauthorized - Authentication is required.",
                403 => "Forbidden - You do not have permission to access this resource.",
                404 => "Not Found - The requested resource was not found.",
                429 => "Too Many Requests - Rate limit exceeded.",
                500 => "Internal Server Error - The server encountered an unexpected condition.",
                502 => "Bad Gateway - Invalid response from upstream server.",
                503 => "Service Unavailable - The server is temporarily unavailable.",
                504 => "Gateway Timeout - The server did not respond in time.",
                _ => "HTTP Error"
            };
        }
    }
}
