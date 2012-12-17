using System;
using System.Collections.Generic;
using Gehtsoft.PCRE;

namespace Alphashack.Graphdat.Agent.SqlQueryHelper
{
    public class Parser
    {
        public const RegexOption DefaultOption = RegexOption.CaseLess;

        private class Replacement
        {
            public int Start;
            public int Length;
            public string Value;
        }

        /****************************************
        * Matches
        *
        * returns starting index of match or -1
        */
        public int Matches(string subject, string regex, RegexOption option = DefaultOption)
        {
            var r = new Regex(regex, option);
            var match = r.Execute(subject);
            if (match.Success)
                return match.Groups[0].Start;
            return -1;
        }

        /****************************************
        * Matching
        *
        * returns matching substring if found, null if not or if group index is out of range (group 0 is entire match, 1 is first group etc.)
        */
        public string Matching(string subject, string regex, RegexOption option = DefaultOption, int group = 0)
        {
            var r = new Regex(regex, option);
            var match = r.Execute(subject);

            if (match.Success && match.Groups.Count > group)
                return match.Groups[group].Value;
            return null;
        }

        /****************************************
        * Substitute
        *
        * Replaces all occurrences of regex with replacement string
        */
        public string Substitute(string subject, string regex, string replace, RegexOption option = DefaultOption, int offset = 0)
        {
            // Special cases, if $N is passed then use the substring as the replacement
            // if #N is passed then use substring, pad with a space on either side
            var group = -1;
            var substituteGroup = replace.Length == 2 && (replace[0] == '$' || replace[0] == '#') &&
                                   int.TryParse(replace[1].ToString(), out group);
            var pad = !substituteGroup ? false : replace[0] == '#';

            var r = new Regex(regex, option);
            var match = r.Execute(subject);

            var replacements = new List<Replacement>();

            while (match.Success)
            {
                if (match.Groups[0].Start >= offset)
                {
                    var doReplace = false;
                    if (substituteGroup)
                    {
                        if (match.Groups.Count > group)
                        {
                            doReplace = true;
                            replace = string.Format("{0}{1}{0}", pad ? " " : "", match.Groups[group].Value);
                        }
                    }
                    else
                    {
                        doReplace = true;
                    }

                    if (doReplace)
                    {
                        replacements.Add(new Replacement
                                             {
                                                 Start = match.Groups[0].Start,
                                                 Length = match.Groups[0].Length,
                                                 Value = replace
                                             });
                    }
                }

                match = match.NextMatch;
            }

            replacements.Reverse();
            foreach (var replacement in replacements)
            {
                subject = subject.Remove(replacement.Start, replacement.Length);
                subject = subject.Insert(replacement.Start, replacement.Value);
            }

            return subject;
        }

        /****************************************
        * mysql_query_rewrite
        */
        public string Rewrite(string query)
        {
            string result;

            // Stored proc calls, remove args
            if ((result = Matching(query, "\\A\\s*((?:exec|execute)\\s+\\S+)", group: 1)) != null)
            {
                return result.ToLower();
            }

            // Remove one line comments
            query = Substitute(query, "(?:--|#)[^'\\r\\n]*([\\r\\n]|\\Z)", "");

            // Remove multiline comments
            query = Substitute(query, "/\\*[^!].*?\\*/", "", DefaultOption | RegexOption.DotAll | RegexOption.MultiLine);

            // Generalize use
            query = Substitute(query, "\\A\\s*use \\S+\\Z", "use ?");

            // Quoted strings
            query = Substitute(query, "\\\\[\"']", "");
            query = Substitute(query, "\".*?\"", "?", DefaultOption | RegexOption.DotAll);
            query = Substitute(query, "'.*?'", "?", DefaultOption | RegexOption.DotAll);

            // MD5s
            query = Substitute(query, "([._-])[a-f0-9]{32}", "?");

            // Numbers
            query = Substitute(query, "\\b[0-9+-][0-9a-f.xb+-]*", "?");

            // Tidy
            query = Substitute(query, "[xb+-]\\?", "?");

            // Remove leading ws
            query = Substitute(query, "\\A\\s+", "");

            // Remove trailing ws
            query = Substitute(query, "[\\r\\n\\s]+$", "");

            // Collapse internal ws
            query = Substitute(query, "[ \\n\\t\\r\\f]+", " ", DefaultOption | RegexOption.DotAll);

            // Null
            query = Substitute(query, "\\bnull\\b", "?");
            query = Substitute(query, "\\bis\\s+not\\s+\\?", "?null?");
            query = Substitute(query, "\\bis\\s+\\?", "?null?");

            // Collapse IN
            query = Substitute(query, "\\bin(?:[\\s,]*\\([\\s?,]*\\))+", "in (?)");

            // Collapse VALUES
            query = Substitute(query, "\\bvalues(?:[\\s,]*\\([\\s?,]*\\))+", "values (?)");

            // Collapse repeated select UNION
            query = Substitute(query, "\\b(select\\s.*?)(?:(\\sunion(?:\\sall)?)\\s\\1)+", "$1");

            // Tighten up operators
            query = Substitute(query, "\\s?([,=+*/-])\\s?", "#1");

            return query.ToLower();
        }
    }
}
