﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.DocumentHighlighting;
using Microsoft.CodeAnalysis.FindUsages;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.VisualStudio.Shell.FindAllReferences;
using Microsoft.VisualStudio.Shell.TableControl;

namespace Microsoft.VisualStudio.LanguageServices.FindUsages
{
    internal partial class StreamingFindUsagesPresenter
    {
        /// <summary>
        /// Context to be used for FindImplementations/GoToDef (as opposed to FindReferences).
        /// This context will not group entries by definition, and will instead just create
        /// entries for the definitions themselves.
        /// </summary>
        private class WithoutReferencesFindUsagesContext : AbstractTableDataSourceFindUsagesContext
        {
            public WithoutReferencesFindUsagesContext(
                StreamingFindUsagesPresenter presenter,
                IFindAllReferencesWindow findReferencesWindow,
                ImmutableArray<ITableColumnDefinition> customColumns,
                bool includeContainingTypeAndMemberColumns,
                bool includeKindColumn)
                : base(presenter, findReferencesWindow, customColumns, includeContainingTypeAndMemberColumns, includeKindColumn)
            {
            }

            // We should never be called in a context where we get references.
            protected override Task OnReferenceFoundWorkerAsync(SourceReferenceItem reference)
                => throw new InvalidOperationException();

            // Nothing to do on completion.
            protected override Task OnCompletedAsyncWorkerAsync()
                => Task.CompletedTask;

            protected override async Task OnDefinitionFoundWorkerAsync(DefinitionItem definition)
            {
                var definitionBucket = GetOrCreateDefinitionBucket(definition);

                var entries = ArrayBuilder<Entry>.GetInstance();

                if (definition.SourceSpans.Length == 1)
                {
                    // If we only have a single location, then use the DisplayParts of the
                    // definition as what to show.  That way we show enough information for things
                    // methods.  i.e. we'll show "void TypeName.MethodName(args...)" allowing
                    // the user to see the type the method was created in.
                    var entry = await TryCreateEntryAsync(definitionBucket, definition).ConfigureAwait(false);
                    entries.AddIfNotNull(entry);
                }
                else if (definition.SourceSpans.Length == 0)
                {
                    // No source spans means metadata references.
                    // Display it for Go to Base and try to navigate to metadata.
                    entries.Add(new MetadataDefinitionItemEntry(this, definitionBucket));
                }
                else
                {
                    // If we have multiple spans (i.e. for partial types), then create a 
                    // DocumentSpanEntry for each.  That way we can easily see the source
                    // code where each location is to help the user decide which they want
                    // to navigate to.
                    foreach (var sourceSpan in definition.SourceSpans)
                    {
                        var entry = await TryCreateDocumentSpanEntryAsync(
                            definitionBucket,
                            sourceSpan,
                            HighlightSpanKind.Definition,
                            symbolUsageInfo: SymbolUsageInfo.None,
                            additionalProperties: definition.DisplayableProperties)
                                .ConfigureAwait(false);
                        entries.AddIfNotNull(entry);
                    }
                }

                if (entries.Count > 0)
                {
                    lock (Gate)
                    {
                        EntriesWhenGroupingByDefinition = EntriesWhenGroupingByDefinition.AddRange(entries);
                        EntriesWhenNotGroupingByDefinition = EntriesWhenNotGroupingByDefinition.AddRange(entries);
                    }

                    NotifyChange();
                }

                entries.Free();
            }

            private async Task<Entry> TryCreateEntryAsync(
                RoslynDefinitionBucket definitionBucket, DefinitionItem definition)
            {
                var documentSpan = definition.SourceSpans[0];
                var (projectGuid, documentGuid, projectName, sourceText) = await FindUsagesUtilities.GetGuidAndProjectNameAndSourceTextAsync(documentSpan.Document, CancellationToken).ConfigureAwait(false);

                var lineText = FindUsagesUtilities.GetLineContainingPosition(sourceText, documentSpan.SourceSpan.Start);
                var mappedDocumentSpan = await FindUsagesUtilities.TryMapAndGetFirstAsync(documentSpan, sourceText, CancellationToken).ConfigureAwait(false);
                if (mappedDocumentSpan == null)
                {
                    // this will be removed from the result
                    return null;
                }

                return new DefinitionItemEntry(this, definitionBucket, projectName, projectGuid, lineText, mappedDocumentSpan.Value);
            }
        }
    }
}
