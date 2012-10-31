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
        private static readonly IList<Type> HandledStatementType = new[]
                                                                       {
                                                                           typeof (SelectStatement),
                                                                           typeof(UpdateStatement)
                                                                       };

        private static readonly IList<TSqlTokenType> ReplaceTokens = new[]
                                                                         {
                                                                             TSqlTokenType.AsciiStringLiteral,
                                                                             TSqlTokenType.HexLiteral,
                                                                             TSqlTokenType.UnicodeStringLiteral,
                                                                             TSqlTokenType.Integer,
                                                                             TSqlTokenType.Money
                                                                         };
        private static readonly char[] RemoveChars = new [] {'\r', '\n', ';'};

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

        public static bool ParseSql(string query, out string value)
        {
            value = "";

            var parser = new TSql100Parser(true);
            IList<ParseError> errors;
            var result = parser.Parse(new StringReader(query), out errors);

            if(errors.Count > 0)
                return false;

            var script = result as TSqlScript;
            if (script == null || script.Batches.Count <= 0 && script.Batches[0].Statements.Count <= 0)
                return false;

            //if (!HandledStatementType.Contains(script.Batches[0].Statements[0].GetType()))
            //    return false;

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
                if(ReplaceTokens.Contains(token.TokenType))
                {
                    summary.Append(token.TokenType);
                }
                else
                {
                    summary.Append(token.Text);
                }
            }
            value = summary.ToString().TrimEnd(RemoveChars);
            return true;
        }
    }
}
