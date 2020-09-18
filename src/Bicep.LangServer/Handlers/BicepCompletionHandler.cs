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
using Bicep.LanguageServer.Utils;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using CompletionContext = Bicep.LanguageServer.Completions.CompletionContext;

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
                return Task.FromResult<CompletionList>(new CompletionList());
            }

            int offset = PositionHelper.GetOffset(compilationContext.LineStarts, request.Position);
            var completionContext = CompletionContext.Create(compilationContext.Compilation.ProgramSyntax, offset);

            var completions = GetKeywordCompletions(completionContext)
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

        private IEnumerable<CompletionItem> GetKeywordCompletions(CompletionContext completionContext)
        {
            if (completionContext.Kind.HasFlag(CompletionContextKind.Declaration))
            {
                yield return CreateKeywordCompletion(LanguageConstants.ParameterKeyword);
                yield return CreateKeywordCompletion(LanguageConstants.VariableKeyword);
                yield return CreateKeywordCompletion(LanguageConstants.ResourceKeyword);
                yield return CreateKeywordCompletion(LanguageConstants.OutputKeyword);
            }
        }

        private IEnumerable<CompletionItem> GetSymbolCompletions(CompilationContext compilationContext, CompletionContext completionContext)
        {
            if (completionContext.Kind.HasFlag(CompletionContextKind.Declaration) == false)
            {
                var model = compilationContext.Compilation.GetSemanticModel();
                return GetAccessibleSymbols(model).Select(sym => sym.ToCompletionItem());
            }

            return Enumerable.Empty<CompletionItem>();
        }

        private IEnumerable<CompletionItem> GetPrimitiveTypeCompletions(CompletionContext completionContext) =>
            completionContext.Kind.HasFlag(CompletionContextKind.Declaration)
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

        private static CompletionItem CreateKeywordCompletion(string keyword) =>
            new CompletionItem
            {
                Kind = CompletionItemKind.Keyword,
                Label = keyword,
                InsertTextFormat = InsertTextFormat.PlainText,
                InsertText = keyword,
                CommitCharacters = new Container<string>(" "),
                Detail = keyword
            };

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
