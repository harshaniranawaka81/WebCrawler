using System;
using System.Collections.Generic;
using System.Linq;

namespace WebCrawler.Infrastructure.Helpers
{
    /// <summary>
    /// Parses robots.txt files and extracts rules.
    /// Supports basic User-Agent matching and Disallow/Allow rules.
    /// </summary>
    public static class RobotsParser
    {
        /// <summary>
        /// Represents a set of robots.txt rules for a specific user agent
        /// </summary>
        public class RobotRules
        {
            public string UserAgent { get; set; } = "*";
            public List<string> DisallowPaths { get; set; } = [];
            public List<string> AllowPaths { get; set; } = [];
            public int? CrawlDelay { get; set; }
            public int? RequestRate { get; set; }
        }

        /// <summary>
        /// Parses robots.txt content and extracts rules.
        /// Prioritizes specific user agents (e.g., "WebCrawler") over wildcard "*".
        /// </summary>
        /// <param name="robotsContent">The complete content of robots.txt file</param>
        /// <param name="userAgent">The user agent name to match (default: wildcard "*")</param>
        /// <returns>Applicable robot rules for the given user agent</returns>
        public static RobotRules Parse(string robotsContent, string userAgent = "*")
        {
            if (string.IsNullOrWhiteSpace(robotsContent))
            {
                return new RobotRules { UserAgent = userAgent };
            }

            var lines = robotsContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var rulesByAgent = new Dictionary<string, RobotRules>(StringComparer.OrdinalIgnoreCase);

            RobotRules? currentRules = null;
            var wildcardRules = new RobotRules { UserAgent = "*" };
            var specificRules = new RobotRules { UserAgent = userAgent };

            foreach (var line in lines)
            {
                // Remove comments
                var cleanLine = RemoveComments(line).Trim();

                if (string.IsNullOrEmpty(cleanLine))
                    continue;

                var (directive, value) = ParseDirective(cleanLine);

                if (directive.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
                {
                    // Check if this is a matching user agent
                    if (value.Equals(userAgent, StringComparison.OrdinalIgnoreCase))
                    {
                        currentRules = specificRules;
                    }
                    else if (value.Equals("*", StringComparison.OrdinalIgnoreCase))
                    {
                        currentRules = wildcardRules;
                    }
                    else
                    {
                        // Store rules for other user agents but don't use them
                        if (!rulesByAgent.ContainsKey(value))
                        {
                            rulesByAgent[value] = new RobotRules { UserAgent = value };
                        }
                        currentRules = rulesByAgent[value];
                    }
                }
                else if (currentRules != null)
                {
                    if (directive.Equals("Disallow", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(value))
                        {
                            currentRules.DisallowPaths.Add(value);
                        }
                    }
                    else if (directive.Equals("Allow", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(value))
                        {
                            currentRules.AllowPaths.Add(value);
                        }
                    }
                    else if (directive.Equals("Crawl-delay", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(value, out int delay))
                        {
                            currentRules.CrawlDelay = delay;
                        }
                    }
                    else if (directive.Equals("Request-rate", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(value, out int rate))
                        {
                            currentRules.RequestRate = rate;
                        }
                    }
                }
            }

            // Prefer specific rules over wildcard
            if (specificRules.DisallowPaths.Count > 0 || specificRules.AllowPaths.Count > 0)
            {
                return specificRules;
            }

            return wildcardRules;
        }

        /// <summary>
        /// Checks if a path is allowed by the robot rules.
        /// Allow rules take precedence over Disallow rules.
        /// </summary>
        /// <param name="rules">The robot rules to check against</param>
        /// <param name="path">The request path to check (e.g., "/page.html")</param>
        /// <returns>True if path is allowed, false if disallowed</returns>
        public static bool IsPathAllowed(RobotRules rules, string path)
        {
            if (rules == null || (rules.DisallowPaths.Count == 0 && rules.AllowPaths.Count == 0))
            {
                return true; // No restrictions
            }

            // Normalize path
            path = path.Split('?')[0]; // Remove query string
            path = path.StartsWith("/") ? path : "/" + path;

            // Check Allow rules first (they have higher priority)
            foreach (var allowPath in rules.AllowPaths)
            {
                if (PathMatches(path, allowPath))
                {
                    return true;
                }
            }

            // Check Disallow rules
            foreach (var disallowPath in rules.DisallowPaths)
            {
                if (PathMatches(path, disallowPath))
                {
                    return false;
                }
            }

            return true; // Not matched by any rule, allowed
        }

        /// <summary>
        /// Checks if a path matches a robots.txt rule pattern.
        /// Supports wildcards (*) at the end of patterns.
        /// </summary>
        private static bool PathMatches(string path, string pattern)
        {
            // Handle empty pattern
            if (string.IsNullOrEmpty(pattern))
            {
                return pattern == "/";
            }

            // Exact match
            if (pattern == path)
            {
                return true;
            }

            // Wildcard pattern (e.g., "/admin/*" or "/private*")
            if (pattern.EndsWith("*"))
            {
                var prefix = pattern.Substring(0, pattern.Length - 1);
                return path.StartsWith(prefix);
            }

            // Prefix match (e.g., "/admin" matches "/admin", "/admin/", "/admin/page.html")
            if (path.StartsWith(pattern))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes comments from a robots.txt line
        /// </summary>
        private static string RemoveComments(string line)
        {
            var commentIndex = line.IndexOf('#');
            return commentIndex >= 0 ? line.Substring(0, commentIndex) : line;
        }

        /// <summary>
        /// Parses a directive line into directive and value
        /// </summary>
        private static (string directive, string value) ParseDirective(string line)
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex < 0)
                return ("", "");

            var directive = line.Substring(0, colonIndex).Trim();
            var value = line.Substring(colonIndex + 1).Trim();
            return (directive, value);
        }
    }
}
