using System;

namespace JoinLeaveAlerts
{
    public static class PlayerNameResolver
    {
        public static string Choose(string connectionName, string authenticatedUsername)
        {
            string connection = Normalize(connectionName);
            if (connection != null) return connection;
            return Normalize(authenticatedUsername);
        }

        private static string Normalize(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return null;
            string normalized = name.Trim();
            return String.Equals(normalized, "Unknown Player", StringComparison.OrdinalIgnoreCase) ? null : normalized;
        }
    }
}
