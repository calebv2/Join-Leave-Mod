using System;
using System.Collections.Generic;
using System.Linq;

namespace JoinLeaveAlerts
{
    public static class ModPackRejectionFormatter
    {
        public static string Format(string template, string playerName, IEnumerable<string> problems)
        {
            if (String.IsNullOrWhiteSpace(template) || String.IsNullOrWhiteSpace(playerName)) return null;
            string normalizedName = playerName.Trim().Replace("\r", String.Empty).Replace("\n", String.Empty);
            string normalizedProblems = String.Join("; ", (problems ?? Enumerable.Empty<string>()).Where(problem => !String.IsNullOrWhiteSpace(problem)).Select(problem => problem.Trim()).ToArray());
            if (normalizedName.Length == 0 || normalizedProblems.Length == 0) return null;
            return template.Replace("{player}", normalizedName).Replace("{mods}", normalizedProblems);
        }
    }
}
