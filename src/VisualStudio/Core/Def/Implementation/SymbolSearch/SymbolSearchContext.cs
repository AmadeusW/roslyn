﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.FindUsages;
using Microsoft.VisualStudio.Language.Intellisense.SymbolSearch;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.SymbolSearch
{
    internal class SymbolSearchContext : FindUsagesContext
    {
        internal SymbolSearchSource SymbolSource { get; }
        internal SymbolOrigin LocalOrigin { get; }

        private readonly ISymbolSearchCallback Callback;
        public new readonly CancellationToken CancellationToken;
        private readonly Dictionary<DefinitionItem, RoslynSymbolSearchResult> DefinitionResults = new Dictionary<DefinitionItem, RoslynSymbolSearchResult>();
        private object Gate = new object();

        public SymbolSearchContext(SymbolSearchSource symbolSource, ISymbolSearchCallback callback, string rootNodeName, CancellationToken token)
        {
            this.SymbolSource = symbolSource;
            this.Callback = callback;
            this.CancellationToken = token;
            this.LocalOrigin = new SymbolOrigin(PredefinedSymbolOriginIds.LocalCode, rootNodeName, string.Empty, default);
        }

        public override async Task OnDefinitionFoundAsync(DefinitionItem definition)
        {
            if (definition.SourceSpans.Length == 1)
            {
                var result = await RoslynSymbolSearchResult.MakeAsync(this, definition, definition.SourceSpans[0], CancellationToken)
                    .ConfigureAwait(false);
                this.Callback.AddRange(ImmutableArray.Create<SymbolSearchResult>(result));
                lock (Gate)
                {
                    this.DefinitionResults.Add(definition, result);
                }
            }
            else if (definition.SourceSpans.Length > 1)
            {
                // Create a definition for each of the parts
                var builder = ImmutableArray.CreateBuilder<SymbolSearchResult>(definition.SourceSpans.Length);
                foreach (var sourceSpan in definition.SourceSpans)
                {
                    var result = await RoslynSymbolSearchResult.MakeAsync(this, definition, sourceSpan, CancellationToken)
                        .ConfigureAwait(false);
                    builder.Add(result);
                }
                this.Callback.AddRange(builder.ToImmutable());
            }

            return;
        }

        public override async Task OnReferenceFoundAsync(SourceReferenceItem reference)
        {
            var result = await RoslynSymbolSearchResult.MakeAsync(this, reference, reference.SourceSpan, CancellationToken)
                .ConfigureAwait(false);
            this.Callback.AddRange(ImmutableArray.Create<SymbolSearchResult>(result));
            return;
        }

        internal RoslynSymbolSearchResult GetDefinitionResult(DefinitionItem definition)
        {
            lock (Gate)
            {
                if (DefinitionResults.TryGetValue(definition, out var definitionResult))
                {
                    return definitionResult;
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
