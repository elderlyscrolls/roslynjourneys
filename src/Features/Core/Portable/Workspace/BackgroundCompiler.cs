﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Collections;
using Microsoft.CodeAnalysis.PooledObjects;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Host
{
    internal sealed class BackgroundCompiler : IDisposable
    {
        private Workspace? _workspace;
        private readonly AsyncBatchingWorkQueue<CancellationToken> _workQueue;

        private readonly object _gate = new();
        [SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Used to keep a strong reference to the built compilations so they are not GC'd")]
        private readonly List<Compilation> _mostRecentCompilations = new();

        private readonly CancellationSeries _cancellationSeries = new();

        private readonly CancellationTokenSource _disposalCancellationSource = new();

        public BackgroundCompiler(Workspace workspace)
        {
            _workspace = workspace;

            // make a scheduler that runs on the thread pool
            var listenerProvider = workspace.Services.GetRequiredService<IWorkspaceAsynchronousOperationListenerProvider>();
            _workQueue = new AsyncBatchingWorkQueue<CancellationToken>(
                DelayTimeSpan.NearImmediate,
                BuildCompilationsForVisibleDocumentsAsync,
                listenerProvider.GetListener(),
                _disposalCancellationSource.Token);

            _workspace.WorkspaceChanged += OnWorkspaceChanged;
            _workspace.DocumentOpened += OnDocumentOpened;
            _workspace.DocumentClosed += OnDocumentClosed;
        }

        public void Dispose()
        {
            _disposalCancellationSource.Cancel();
            _cancellationSeries.Dispose();

            lock (_gate)
            {
                _mostRecentCompilations.Clear();
            }

            if (_workspace != null)
            {
                _workspace.DocumentClosed -= OnDocumentClosed;
                _workspace.DocumentOpened -= OnDocumentOpened;
                _workspace.WorkspaceChanged -= OnWorkspaceChanged;

                _workspace = null;
            }
        }

        private void OnDocumentOpened(object? sender, DocumentEventArgs args)
            => Rebuild();

        private void OnDocumentClosed(object? sender, DocumentEventArgs args)
            => Rebuild();

        private void OnWorkspaceChanged(object? sender, WorkspaceChangeEventArgs args)
            => Rebuild();

        private void Rebuild()
        {
            var nextToken = _cancellationSeries.CreateNext();
            _workQueue.AddWork(nextToken);
        }

        private async ValueTask BuildCompilationsForVisibleDocumentsAsync(
            ImmutableSegmentedList<CancellationToken> cancellationTokens, CancellationToken disposalToken)
        {
            var workspace = _workspace;
            if (workspace is null)
                return;

            // Because we always cancel the previous token prior to queuing new work, there can only be at most one
            // actual real cancellation token that is not already canceled.
            var cancellationToken = cancellationTokens.SingleOrDefault(ct => !ct.IsCancellationRequested);

            // if we didn't get an actual non-canceled token back, then this batch was entirely canceled and we have
            // nothing to do.
            if (cancellationToken == default)
                return;

            using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disposalToken);
            await BuildCompilationsForVisibleDocumentsAsync(workspace.CurrentSolution, source.Token).ConfigureAwait(false);
        }

        private async ValueTask BuildCompilationsForVisibleDocumentsAsync(
            Solution solution, CancellationToken cancellationToken)
        {
            var trackingService = solution.Workspace.Services.GetRequiredService<IDocumentTrackingService>();
            var visibleProjectIds = trackingService.GetVisibleDocuments().Select(d => d.ProjectId).ToSet();
            var activeProjectId = trackingService.TryGetActiveDocument()?.ProjectId;

            using var _ = ArrayBuilder<Compilation>.GetInstance(out var compilations);

            await GetCompilationAsync(activeProjectId).ConfigureAwait(false);

            foreach (var projectId in visibleProjectIds)
            {
                if (projectId != activeProjectId)
                {
                    await GetCompilationAsync(projectId).ConfigureAwait(false);
                }
            }

            lock (_gate)
            {
                _mostRecentCompilations.Clear();
                _mostRecentCompilations.AddRange(compilations);
            }

            return;

            async ValueTask GetCompilationAsync(ProjectId? projectId)
            {
                var project = solution.GetProject(projectId);
                if (project is null)
                    return;

                var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
                if (compilation is null)
                    return;

                compilations.AddIfNotNull(compilation);
            }
        }
    }
}
