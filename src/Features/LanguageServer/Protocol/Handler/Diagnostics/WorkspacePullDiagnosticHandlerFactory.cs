﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.LanguageServer.Handler.Diagnostics;

[ExportCSharpVisualBasicLspServiceFactory(typeof(WorkspacePullDiagnosticHandler)), Shared]
[method: ImportingConstructor]
[method: Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
internal class WorkspacePullDiagnosticHandlerFactory(
    LspWorkspaceRegistrationService registrationService,
    IDiagnosticAnalyzerService analyzerService,
    IDiagnosticsRefresher diagnosticsRefresher,
    IGlobalOptionService globalOptions) : ILspServiceFactory
{
    public ILspService CreateILspService(LspServices lspServices, WellKnownLspServerKinds serverKind)
    {
        var workspaceManager = lspServices.GetRequiredService<LspWorkspaceManager>();
        return new WorkspacePullDiagnosticHandler(workspaceManager, registrationService, analyzerService, diagnosticsRefresher, globalOptions);
    }
}
