using System;
using Microsoft.Extensions.Logging;
using WebCrawler.Core.Interfaces;

namespace WebCrawler.Core.Services
{
    /// <summary>
    /// Centralized exception handler for the application
    /// Handles all exceptions that occur during crawling operations
    /// </summary>
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(IOutputWriter outputWriter, ILogger<GlobalExceptionHandler>? logger = null)
        {
            _ = outputWriter ?? throw new ArgumentNullException(nameof(outputWriter));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance.CreateLogger<GlobalExceptionHandler>();
        }

        /// <summary>
        /// Handles exceptions by categorizing them and displaying appropriate messages
        /// </summary>
        public void Handle(Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            _logger.LogError(exception, "An exception occurred in the application");
            Console.WriteLine();

            switch (exception)
            {
                case HttpRequestException httpEx:
                    HandleHttpException(httpEx);
                    break;

                case OperationCanceledException cancelEx:
                    HandleTimeoutException(cancelEx);
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
        /// Executes an action that returns a result with global exception handling
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

        #region Exception Type Handlers

        private void HandleHttpException(HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request exception occurred");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ NETWORK ERROR");
            Console.ResetColor();

            if (ex.InnerException is TimeoutException)
            {
                Console.WriteLine("   └─ Request timed out. The server took too long to respond.");
            }
            else if (ex.StatusCode.HasValue)
            {
                Console.WriteLine($"   └─ HTTP {(int)ex.StatusCode}: {ex.StatusCode}");
                Console.WriteLine($"      {GetHttpStatusDescription((int)ex.StatusCode)}");
            }
            else
            {
                Console.WriteLine($"   └─ Connection failed: {ex.Message}");
            }
        }

        private void HandleTimeoutException(OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Operation timeout exception occurred");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⏱️  TIMEOUT ERROR");
            Console.ResetColor();
            Console.WriteLine("   └─ Request timeout. The operation took too long to complete.");
            Console.WriteLine("   └─ Check your internet connection or increase the timeout setting.");
        }

        private void HandleUriException(UriFormatException ex)
        {
            _logger.LogError(ex, "URI format exception occurred");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ INVALID URL FORMAT");
            Console.ResetColor();
            Console.WriteLine($"   └─ The URL provided is not in a valid format.");
            Console.WriteLine($"   └─ Please ensure the URL starts with 'http://' or 'https://'");
        }

        private void HandleArgumentException(ArgumentException ex)
        {
            _logger.LogError(ex, "Argument exception occurred for parameter: {ParamName}", ex.ParamName);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ INVALID ARGUMENT");
            Console.ResetColor();
            Console.WriteLine($"   └─ {ex.Message}");
            Console.WriteLine($"   └─ Parameter: {ex.ParamName}");
        }

        private void HandleInvalidOperationException(InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation exception occurred");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠️  OPERATION ERROR");
            Console.ResetColor();
            Console.WriteLine($"   └─ {ex.Message}");
        }

        private void HandleGenericException(Exception ex)
        {
            _logger.LogCritical(ex, "Unexpected exception occurred");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ UNEXPECTED ERROR");
            Console.ResetColor();
            Console.WriteLine($"   └─ {ex.GetType().Name}: {ex.Message}");

            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("\n   Stack Trace:");
                foreach (var line in ex.StackTrace.Split(Environment.NewLine))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        Console.WriteLine($"   {line}");
                }
                Console.ResetColor();
            }
        }

        #endregion

        #region Helper Methods

        private string GetHttpStatusDescription(int statusCode)
        {
            return statusCode switch
            {
                400 => "Bad Request - The request was malformed",
                401 => "Unauthorized - Authentication required",
                403 => "Forbidden - Access denied",
                404 => "Not Found - The resource does not exist",
                429 => "Too Many Requests - Rate limit exceeded",
                500 => "Internal Server Error - Server encountered an error",
                502 => "Bad Gateway - Invalid response from upstream server",
                503 => "Service Unavailable - Server is temporarily unavailable",
                504 => "Gateway Timeout - Upstream server timeout",
                _ => "HTTP Error"
            };
        }

        #endregion
    }
}
