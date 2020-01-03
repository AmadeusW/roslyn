﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.VisualStudio.Language.Intellisense.SymbolSearch;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.SymbolSearch
{
    [Export(typeof(ISymbolSourceProvider))]
    [ContentType(ContentTypeNames.RoslynContentType)]
    [Name(nameof(SymbolSearchSourceProvider))]
    internal class SymbolSearchSourceProvider : ISymbolSourceProvider
    {
        [Import]
        internal IPersistentSpanFactory PersistentSpanFactory { get; private set; }

        [Import]
        internal ISymbolSearchBroker SymbolSearchBroker { get; private set; }

        /// <summary>
        /// <see cref="ISymbolSourceProvider.Create(ITextBuffer)"/> is capable of returning multiple <see cref="ISymbolSource"/>s,
        /// and each source can return <see cref="SymbolSearchResult"/>s with varying <see cref="SymbolOrigin"/>.
        /// Currently, we are using only <see cref="SymbolSearchSourceProvider"/> 
        /// and will cache the return value of <see cref="ISymbolSourceProvider.Create(ITextBuffer)"/> in this variable.
        /// </summary>
        private ImmutableArray<ISymbolSource> _cachedSources;

        private object _cacheLock = new object();

        IEnumerable<ISymbolSource> ISymbolSourceProvider.Create(ITextBuffer buffer)
        {
            lock (_cacheLock)
            {
                if (_cachedSources == default)
                {
                    _cachedSources = ImmutableArray.Create<ISymbolSource>(new SymbolSearchSource(this));
                }
            }

            return _cachedSources;
        }
    }
}
