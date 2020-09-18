// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Bicep.Core;
using Bicep.Core.Navigation;
using Bicep.Core.Parser;
using Bicep.Core.Syntax;

namespace Bicep.LanguageServer.Completions
{
    public class CompletionContext
    {
        public CompletionContext(CompletionContextKind kind)
        {
            this.Kind = kind;
        }

        public CompletionContextKind Kind { get; }

        public static CompletionContext Create(ProgramSyntax syntax, int offset)
        {
            var node = syntax.TryFindMostSpecificNodeExclusive(offset, current => !(current is Token));
            if (node == null)
            {
                return new CompletionContext(CompletionContextKind.None);
            }

            var kind = IsDeclarationContext(syntax, offset, node) ? CompletionContextKind.Declaration : CompletionContextKind.None;

            return new CompletionContext(kind);
        }

        private static bool IsDeclarationContext(ProgramSyntax syntax, int offset, SyntaxBase mostSpecificNode) => mostSpecificNode is ProgramSyntax;

        private static int IndexOf(IList<Token> tokens, int offset)
        {
            for(int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Span.Contains(offset))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
