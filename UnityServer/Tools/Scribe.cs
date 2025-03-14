using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace UnityServer.Tools
{
    /// <summary>
    /// Utility class for logging messages and errors to the console.
    /// Provides structured output with timestamps and caller information.
    /// </summary>
    public static class Scribe
    {
        /// <summary>
        /// Logs an informational message to the console.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="caller">The calling method (automatically captured).</param>
        public static void Write(string message, [CallerMemberName] string caller = "")
        {
            Console.WriteLine($"[ {Timestamp()} ] - [ {caller} ] - [ INFO ] - [ {message} ]");
        }

        /// <summary>
        /// Logs an error message to the console, including exception details.
        /// </summary>
        /// <param name="ex">The exception to log.</param>
        /// <param name="caller">The calling method (automatically captured).</param>
        public static void Error(Exception ex, [CallerMemberName] string caller = "")
        {
            Console.ForegroundColor = ConsoleColor.Red;

            Console.Write($"[ {Timestamp()} ] - [ {caller} ] - [ ERROR ] - [ {ex.Message} ]");

            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                Console.Write($" - [ StackTrace: {ex.StackTrace} ]");
            }

            Console.WriteLine();
            Console.ResetColor();
        }

        /// <summary>
        /// Generates a formatted timestamp string.
        /// </summary>
        /// <returns>A timestamp in the format MM.dd.yy HH:mm:ss.ffff</returns>
        public static string Timestamp()
        {
            return DateTime.Now.ToString("MM.dd.yy HH:mm:ss.ffff");
        }
    }
}
