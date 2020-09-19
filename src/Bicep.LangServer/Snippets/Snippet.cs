// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Bicep.Core.Parser;

namespace Bicep.LanguageServer.Snippets
{
    public sealed class Snippet
    {
        private static readonly Regex PlaceholderPattern = new Regex(@"\$({(?<index>\d+)(:(?<name>\w+)})?)", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        public Snippet(string text)
        {
            var matches = PlaceholderPattern.Matches(text);

            this.Text = text;
            this.Placeholders = matches
                .Select(CreatePlaceholder)
                .OrderBy(p=>p.Index)
                .ToImmutableArray();

            this.Validate();
        }

        public string Text { get; }

        // placeholders ordered by index
        public ImmutableArray<SnippetPlaceholder> Placeholders { get; }

        public string FormatDocumentation()
        {
            // we will be performing multiple string replacements
            // better to do it in-place
            var buffer = new StringBuilder(this.Text);

            // placeholders are ordered by index
            // to avoid recomputing spans, we will perform the replacements in reverse order
            foreach (var placeholder in this.Placeholders.Reverse())
            {
                // remove original placeholder
                buffer.Remove(placeholder.Span.Position, placeholder.Span.Length);

                // for named placeholders, insert the placeholder name
                if (placeholder.Name != null)
                {
                    buffer.Insert(placeholder.Span.Position, placeholder.Name);
                }
            }

            return buffer.ToString();
        }

        private static SnippetPlaceholder CreatePlaceholder(Match match) =>
            new SnippetPlaceholder(
                index: int.Parse(match.Groups["index"].Value),
                name: match.Groups.ContainsKey("name") ? match.Groups["name"].Value : null,
                span: new TextSpan(match.Index, match.Length));

        private void Validate()
        {
            // empty snippet is pointless but still valid
            if (this.Placeholders.IsEmpty)
            {
                return;
            }

            var firstPlaceholderIndex = this.Placeholders.First().Index;
            if (firstPlaceholderIndex != 0 && firstPlaceholderIndex != 1)
            {
                throw new ArgumentException($"The first snippet placeholder must have index 0 or 1, but the provided index is {firstPlaceholderIndex}");
            }

            // loop skips first placeholder
            for (int i = 1; i < this.Placeholders.Length; i++)
            {
                var current = this.Placeholders[i];
                var expectedIndex = firstPlaceholderIndex + i;

                if (current.Index != expectedIndex)
                {
                    throw new ArgumentException($"The placeholder indices must be contiguous increasing integers, but the placeholder with index {expectedIndex} is missing.");
                }
            }
        }
    }
}
