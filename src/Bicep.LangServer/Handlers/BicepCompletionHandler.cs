// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bicep.Core;
using Bicep.Core.SemanticModel;
using Bicep.Core.TypeSystem;
using Bicep.LanguageServer.CompilationManager;
using Bicep.LanguageServer.Completions;
using Bicep.LanguageServer.Snippets;
using Bicep.LanguageServer.Utils;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Bicep.LanguageServer.Handlers
{
    public class BicepCompletionHandler : CompletionHandler
    {
        private readonly ICompilationManager compilationManager;

        public BicepCompletionHandler(ICompilationManager compilationManager)
            : base(CreateRegistrationOptions())
        {
            this.compilationManager = compilationManager;
        }

        public override Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            var compilationContext = this.compilationManager.GetCompilation(request.TextDocument.Uri);
            if (compilationContext == null)
            {
                return Task.FromResult(new CompletionList());
            }

            int offset = PositionHelper.GetOffset(compilationContext.LineStarts, request.Position);
            var completionContext = BicepCompletionContext.Create(compilationContext.Compilation.ProgramSyntax, offset);

            var completions = GetDeclarationCompletions(completionContext)
                .Concat(GetSymbolCompletions(compilationContext,completionContext))
                .Concat(GetPrimitiveTypeCompletions(completionContext));
            return Task.FromResult(new CompletionList(completions, isIncomplete: false));
        }

        public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request);
        }

        public override bool CanResolve(CompletionItem value)
        {
            return false;
        }

        private static CompletionRegistrationOptions CreateRegistrationOptions() => new CompletionRegistrationOptions
        {
            DocumentSelector = DocumentSelectorFactory.Create(),
            AllCommitCharacters = new Container<string>(),
            ResolveProvider = false,
            TriggerCharacters = new Container<string>()
        };

        private IEnumerable<CompletionItem> GetDeclarationCompletions(BicepCompletionContext completionContext)
        {
            if (completionContext.Kind.HasFlag(BicepCompletionContextKind.Declaration))
            {
                yield return CreateKeywordCompletion(LanguageConstants.ParameterKeyword, "Parameter keyword");
                yield return CreateSnippetCompletion(LanguageConstants.ParameterKeyword, "param ${1:Identifier} ${2:Type}", "Parameter declaration");
                yield return CreateSnippetCompletion(LanguageConstants.ParameterKeyword, "param ${1:Identifier} ${2:Type} = ${3:DefaultValue}", "Parameter declaration with default value");
                yield return CreateSnippetCompletion(LanguageConstants.ParameterKeyword, "param ${1:Identifier} ${2:Type} {\r\n  default: $3\r\n  allowed: [\r\n    $4\r\n  ]\r\n}\"", "Parameter declaration with default and allowed values");
                yield return CreateSnippetCompletion(LanguageConstants.ParameterKeyword, "param ${1:Identifier} ${2:Type} {\r\n  $0\r\n}", "Parameter declaration with options");
                yield return CreateSnippetCompletion(LanguageConstants.ParameterKeyword, "param ${1:Identifier} string {\r\n  secure: true\r\n}", "Secure string parameter");

                yield return CreateKeywordCompletion(LanguageConstants.VariableKeyword, "Variable keyword");
                yield return CreateSnippetCompletion(LanguageConstants.VariableKeyword, "var ${1:Identifier} = $0", "Variable declaration");

                yield return CreateKeywordCompletion(LanguageConstants.ResourceKeyword, "Resource keyword");
                //yield return CreateSnippetCompletion(LanguageConstants.ResourceKeyword, "var ${1:Identifier} = $0", "Variable declaration");

                yield return CreateKeywordCompletion(LanguageConstants.OutputKeyword, "Output keyword");
                yield return CreateSnippetCompletion(LanguageConstants.OutputKeyword, "output ${1:Identifier} ${2:Type} = $0", "Output declaration");
            }
        }

        private IEnumerable<CompletionItem> GetSymbolCompletions(CompilationContext compilationContext, BicepCompletionContext completionContext)
        {
            if (completionContext.Kind.HasFlag(BicepCompletionContextKind.Declaration) == false)
            {
                var model = compilationContext.Compilation.GetSemanticModel();
                return GetAccessibleSymbols(model).Select(sym => sym.ToCompletionItem());
            }

            return Enumerable.Empty<CompletionItem>();
        }

        private IEnumerable<CompletionItem> GetPrimitiveTypeCompletions(BicepCompletionContext completionContext) =>
            completionContext.Kind.HasFlag(BicepCompletionContextKind.Declaration)
                ? Enumerable.Empty<CompletionItem>()
                : LanguageConstants.DeclarationTypes.Values.Select(CreateTypeCompletion);

        private IEnumerable<Symbol> GetAccessibleSymbols(SemanticModel model)
        {
            var accessibleSymbols = new Dictionary<string, Symbol>();

            // local function
            void AddAccessibleSymbols(IDictionary<string, Symbol> result, IEnumerable<Symbol> symbols)
            {
                foreach (var declaration in symbols)
                {
                    if (result.ContainsKey(declaration.Name) == false)
                    {
                        result.Add(declaration.Name, declaration);
                    }
                }
            }

            AddAccessibleSymbols(accessibleSymbols, model.Root.AllDeclarations
                .Where(decl => !(decl is OutputSymbol)));

            AddAccessibleSymbols(accessibleSymbols, model.Root.ImportedNamespaces
                .SelectMany(ns => ns.Descendants.OfType<FunctionSymbol>()));

            return accessibleSymbols.Values;
        }

        private static CompletionItem CreateKeywordCompletion(string keyword, string detail) =>
            new CompletionItem
            {
                Kind = CompletionItemKind.Keyword,
                Label = keyword,
                InsertTextFormat = InsertTextFormat.PlainText,
                InsertText = keyword,
                CommitCharacters = new Container<string>(" "),
                Detail = detail
            };

        private static CompletionItem CreateSnippetCompletion(string label, string snippet, string detail)
        {
            return new CompletionItem
            {
                Kind = CompletionItemKind.Snippet,
                Label = label,
                InsertTextFormat = InsertTextFormat.Snippet,
                InsertText = snippet,
                Detail = detail,
                Documentation = new StringOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = $"```bicep\n{new Snippet(snippet).FormatDocumentation()}\n```"
                })
            };
        }

        private static CompletionItem CreateTypeCompletion(TypeSymbol type) =>
            new CompletionItem
            {
                Kind = CompletionItemKind.Class,
                Label = type.Name,
                InsertTextFormat = InsertTextFormat.PlainText,
                InsertText = type.Name,
                CommitCharacters = new Container<string>(" "),
                Detail = type.Name
            };
    }
}
