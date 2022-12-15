﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.FindSymbols.Finders;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.FindSymbols
{
    internal partial class FindReferencesSearchEngine
    {
        public async Task FindReferencesInDocumentsAsync(
            ISymbol originalSymbol, IImmutableSet<Document> documents, CancellationToken cancellationToken)
        {
            // Caller needs to pass unidirectional cascading to make this search efficient.  If we only have
            // unidirectional cascading, then we only need to check the potential matches we find in the file against
            // the starting symbol.
            Debug.Assert(_options.UnidirectionalHierarchyCascade);

            var unifiedSymbols = new MetadataUnifyingSymbolHashSet();
            unifiedSymbols.Add(originalSymbol);

            // As we hit symbols, we may have to compute if they have an inheritance relationship to the symbols we're
            // searching for.  Cache those results so we don't have to continually perform them.
            var hasInheritanceRelationshipCache = new ConcurrentDictionary<(ISymbol searchSymbol, ISymbol candidateSymbol), AsyncLazy<bool>>();

            // Create and report the initial set of symbols to search for.  This includes linked and cascaded symbols. It does
            // not walk up/down the inheritance hierarchy.
            var symbolSet = await SymbolSet.DetermineInitialSearchSymbolsAsync(this, unifiedSymbols, cancellationToken).ConfigureAwait(false);
            var allSymbols = symbolSet.ToImmutableArray();
            await ReportGroupsAsync(allSymbols, cancellationToken).ConfigureAwait(false);

            // Process projects in dependency graph order so that any compilations built by one are available for later
            // projects. We only have to examine the projects containing the documents requested though.
            var dependencyGraph = _solution.GetProjectDependencyGraph();
            var projectsToSearch = documents.Select(d => d.Project).ToImmutableHashSet();

            foreach (var projectId in dependencyGraph.GetTopologicallySortedProjects(cancellationToken))
            {
                var currentProject = _solution.GetRequiredProject(projectId);
                if (projectsToSearch.Contains(currentProject))
                    await PerformSearchInProjectAsync(allSymbols, currentProject).ConfigureAwait(false);
            }

            return;

            async Task PerformSearchInProjectAsync(
                ImmutableArray<ISymbol> symbols, Project project)
            {
                using var _1 = PooledDictionary<ISymbol, PooledHashSet<string>>.GetInstance(out var symbolToGlobalAliases);
                try
                {
                    // Compute global aliases up front for the project so it can be used below for all the symbols we're
                    // searching for.
                    await AddGlobalAliasesAsync(project, symbols, symbolToGlobalAliases, cancellationToken).ConfigureAwait(false);

                    foreach (var document in documents)
                    {
                        if (document.Project == project)
                            await PerformSearchInDocumentAsync(symbols, document, symbolToGlobalAliases).ConfigureAwait(false);
                    }
                }
                finally
                {
                    FreeGlobalAliases(symbolToGlobalAliases);
                }
            }

            async Task PerformSearchInDocumentAsync(
                ImmutableArray<ISymbol> symbols,
                Document document,
                PooledDictionary<ISymbol, PooledHashSet<string>> symbolToGlobalAliases)
            {
                // We're doing to do all of our processing of this document at once.  This will necessitate all the
                // appropriate finders checking this document for hits.  We're likely going to need to perform syntax
                // and semantics checks in this file.  So just grab those once here and hold onto them for the lifetime
                // of this call.
                var model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                var root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                var cache = FindReferenceCache.GetCache(model);

                foreach (var symbol in symbols)
                {
                    var globalAliases = GetGlobalAliasesSet(symbolToGlobalAliases, symbol);
                    var state = new FindReferencesDocumentState(document, model, root, cache, globalAliases);

                    await PerformSearchInDocumentWorkerAsync(symbol, document, state).ConfigureAwait(false);
                }
            }

            async Task PerformSearchInDocumentWorkerAsync(
                ISymbol symbol, Document document, FindReferencesDocumentState state)
            {
                // Always perform a normal search, looking for direct references to exactly that symbol.
                foreach (var finder in _finders)
                {
                    var references = await finder.FindReferencesInDocumentAsync(
                        symbol, state, _options, cancellationToken).ConfigureAwait(false);
                    foreach (var (_, location) in references)
                    {
                        var group = await ReportGroupAsync(symbol, cancellationToken).ConfigureAwait(false);
                        await _progress.OnReferenceFoundAsync(group, symbol, location, cancellationToken).ConfigureAwait(false);
                    }
                }

                // Now, for symbols that could involve inheritance, look for references to the same named entity, and
                // see if it's a reference to a symbol that shares an inheritance relationship with that symbol.

                if (InvolvesInheritance(symbol))
                {
                    var tokens = await AbstractReferenceFinder.FindMatchingIdentifierTokensAsync(
                        state, symbol.Name, cancellationToken).ConfigureAwait(false);

                    foreach (var token in tokens)
                    {
                        var parent = state.SyntaxFacts.TryGetBindableParent(token) ?? token.GetRequiredParent();
                        var symbolInfo = state.Cache.GetSymbolInfo(parent, cancellationToken);

                        var (matched, candidate, candidateReason) = await HasInheritanceRelationshipAsync(symbol, symbolInfo).ConfigureAwait(false);
                        if (matched)
                        {
                            // Ensure we report this new symbol/group in case it's the first time we're seeing it.
                            var candidateGroup = await ReportGroupAsync(candidate, cancellationToken).ConfigureAwait(false);

                            var location = AbstractReferenceFinder.CreateReferenceLocation(state, token, candidateReason, cancellationToken);
                            await _progress.OnReferenceFoundAsync(candidateGroup, candidate, location, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }

            async Task<(bool matched, ISymbol candidate, CandidateReason candidateReason)> HasInheritanceRelationshipAsync(
                ISymbol symbol, SymbolInfo symbolInfo)
            {
                if (await HasInheritanceRelationshipSingleAsync(symbol, symbolInfo.Symbol).ConfigureAwait(false))
                    return (matched: true, symbolInfo.Symbol!, CandidateReason.None);

                foreach (var candidate in symbolInfo.CandidateSymbols)
                {
                    if (await HasInheritanceRelationshipSingleAsync(symbol, candidate).ConfigureAwait(false))
                        return (matched: true, candidate, symbolInfo.CandidateReason);
                }

                return default;
            }

            async ValueTask<bool> HasInheritanceRelationshipSingleAsync(ISymbol symbol, ISymbol? candidate)
            {
                if (candidate is null)
                    return false;

                var relationship = hasInheritanceRelationshipCache.GetOrAdd(
                    (symbol.GetOriginalUnreducedDefinition(), candidate.GetOriginalUnreducedDefinition()),
                    t => AsyncLazy.Create(cancellationToken => ComputeInheritanceRelationshipAsync(this, t.searchSymbol, t.candidateSymbol, cancellationToken), cacheResult: true));
                return await relationship.GetValueAsync(cancellationToken).ConfigureAwait(false);
            }

            static async Task<bool> ComputeInheritanceRelationshipAsync(
                FindReferencesSearchEngine engine, ISymbol searchSymbol, ISymbol candidate, CancellationToken cancellationToken)
            {
                // Counter-intuitive, but if these are matching symbols, they do *not* have an inheritance relationship.
                // We do *not* want to report these as they would have been found in the original call to the finders in
                // PerformSearchInTextSpanAsync.
                if (await SymbolFinder.OriginalSymbolsMatchAsync(engine._solution, searchSymbol, candidate, cancellationToken).ConfigureAwait(false))
                    return false;

                // walk upwards from each symbol, seeing if we hit the other symbol.
                var searchSymbolSet = await SymbolSet.CreateAsync(engine, new() { searchSymbol }, cancellationToken).ConfigureAwait(false);
                foreach (var symbolUp in searchSymbolSet.GetAllSymbols())
                {
                    if (await SymbolFinder.OriginalSymbolsMatchAsync(engine._solution, symbolUp, candidate, cancellationToken).ConfigureAwait(false))
                        return true;
                }

                var candidateSymbolSet = await SymbolSet.CreateAsync(engine, new() { candidate }, cancellationToken).ConfigureAwait(false);
                foreach (var candidateUp in candidateSymbolSet.GetAllSymbols())
                {
                    if (await SymbolFinder.OriginalSymbolsMatchAsync(engine._solution, searchSymbol, candidateUp, cancellationToken).ConfigureAwait(false))
                        return true;
                }

                return false;
            }
        }
    }
}
