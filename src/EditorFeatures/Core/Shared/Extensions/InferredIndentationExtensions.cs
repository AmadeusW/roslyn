﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.CodeAnalysis.Editor.Shared.Extensions
{
    internal static class InferredIndentationOptions
    {
        public static async Task<DocumentOptionSet> GetDocumentOptionsWithInferredIndentation(
            this Document document,
            int position,
            bool explicitFormat,
            IIndentationManagerService indentationManagerService,
            CancellationToken cancellationToken)
        {
            var options = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);
            var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var snapshot = text.FindCorrespondingEditorTextSnapshot();

            if (snapshot != null)
            {
                var snapshotPoint = new SnapshotPoint(snapshot, position);
                var buffer = snapshotPoint.Snapshot.TextBuffer;

                options = options.WithChangedOption(FormattingOptions.UseTabs, !indentationManagerService.UseSpacesForWhitespace(buffer, explicitFormat))
                                 .WithChangedOption(FormattingOptions.IndentationSize, indentationManagerService.GetIndentSize(buffer, explicitFormat))
                                 .WithChangedOption(FormattingOptions.TabSize, indentationManagerService.GetTabSize(buffer, explicitFormat));
            }

            return options;
        }
    }
}
