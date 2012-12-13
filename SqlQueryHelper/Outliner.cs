using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Data.Schema.ScriptDom;
using Microsoft.Data.Schema.ScriptDom.Sql;

namespace SqlQueryHelper
{
    public static class Outliner
    {
        // The types of statements to simplify
        private static readonly IList<Type> HandledStatementType = new[]
                                                                       {
                                                                           typeof (DeleteStatement),
                                                                           typeof (SelectStatement),
                                                                           typeof (UpdateStatement),
                                                                           typeof (InsertStatement),
                                                                           typeof (FetchCursorStatement),
                                                                           typeof (BulkInsertStatement),
                                                                           typeof (ExecuteAsStatement),
                                                                           typeof (ExecuteStatement),
                                                                           typeof (InsertBulkStatement),
                                                                           typeof (TruncateTableStatement),
                                                                           typeof (UpdateStatisticsStatement),
                                                                           typeof (UpdateTextStatement)
                                                                       };

        // These tokens will have their values replaced by their type during simplification
        private static readonly IList<TSqlTokenType> ReplaceTokens = new[]
                                                                         {
                                                                             TSqlTokenType.AsciiStringLiteral,
                                                                             TSqlTokenType.HexLiteral,
                                                                             TSqlTokenType.UnicodeStringLiteral,
                                                                             TSqlTokenType.Integer,
                                                                             TSqlTokenType.Money
                                                                         };

        // Chars to just remove from the end of the query
        private static readonly char[] RemoveChars = new [] {'\r', '\n', ';'};
        public static string LastError;

        public static string FirstCommand(string query)
        {
            var left = 0;
            do
            {
                var right = query.IndexOf(' ', left + 1);
                if(right <= left) right = query.Length;
                var token = query.Substring(left, right - left);
                token = token.Replace("\r", "").Replace("\n", "");
                if(IsSqlCommand(token)) return token;
                left = right + 1;
            } while (left < query.Length);
            return "NO_COMMAND_FOUND";
        }

        private static bool IsSqlCommand(string token)
        {
            return true;
        }

        /// <summary>
        /// Simplifies a query by removing the value of certain types of token. Basically the ones that might be parameterised
        /// </summary>
        /// <param name="query"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool TrySimplify(string query, out string value)
        {
            value = "";

            var parser = new TSql100Parser(true);
            IList<ParseError> errors;
            var result = parser.Parse(new StringReader(query), out errors);

            // cannot parse, cannot simplify
            if (errors.Count > 0)
            {
                LastError = "Cannot parse";
                return false;
            }

            // without at least one batch with at least one statement, cannot simplify
            // (should be 1 batch, 1 statement really as we are tracing StatementEnd)
            var script = result as TSqlScript;
            if (script == null || script.Batches.Count <= 0 && script.Batches[0].Statements.Count <= 0)
            {
                LastError = "Not 1 batch 1 statement";
                return false;
            }

            // only interested in certain types of statements (date manipulation ones)
            if (!HandledStatementType.Contains(script.Batches[0].Statements[0].GetType()))
            {
                LastError = "Not handled statement";
                return false;
            }

            // basically remove all comments, newlines and extra whitespace
            var options = new SqlScriptGeneratorOptions
                              {
                                  AlignClauseBodies = false,
                                  AlignColumnDefinitionFields = false,
                                  AlignSetClauseItem = false,
                                  AsKeywordOnOwnLine = false,
                                  IncludeSemicolons = false,
                                  IndentSetClause = false,
                                  IndentViewBody = false,
                                  NewLineBeforeCloseParenthesisInMultilineList = false,
                                  NewLineBeforeFromClause = false,
                                  NewLineBeforeGroupByClause = false,
                                  NewLineBeforeHavingClause = false,
                                  NewLineBeforeJoinClause = false,
                                  NewLineBeforeOpenParenthesisInMultilineList = false,
                                  NewLineBeforeOrderByClause = false,
                                  NewLineBeforeOutputClause = false,
                                  NewLineBeforeWhereClause = false,
                                  MultilineInsertSourcesList = false,
                                  MultilineInsertTargetsList = false,
                                  MultilineSelectElementsList = false,
                                  MultilineSetClauseItems = false,
                                  MultilineViewColumnsList = false,
                                  MultilineWherePredicatesList = false
                              };
            var generator = new Sql100ScriptGenerator(options);
            var tokens = generator.GenerateTokens(script);

            var summary = new StringBuilder();
            foreach (var token in tokens)
            {
                // replace values for parameterisable token types
                if(ReplaceTokens.Contains(token.TokenType))
                {
                    summary.Append("?");
                }
                else
                {
                    summary.Append(token.Text);
                }
            }
            // trim some junk
            value = summary.ToString().TrimEnd(RemoveChars);

            return true;
        }
    }
}
