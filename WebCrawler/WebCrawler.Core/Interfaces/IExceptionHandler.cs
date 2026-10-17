using System;

namespace WebCrawler.Core.Interfaces
{
    /// <summary>
    /// Interface for handling application-wide exceptions
    /// </summary>
    public interface IExceptionHandler
    {
        /// <summary>
        /// Handles exceptions that occur during application execution
        /// </summary>
        void Handle(Exception exception);

        /// <summary>
        /// Executes an action with global exception handling
        /// </summary>
        Task ExecuteAsync(Func<Task> action, string operationName = "Operation");
    }
}
