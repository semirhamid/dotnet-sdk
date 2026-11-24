// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Usage;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage
{
    /// <summary>
    /// CA2268: <inheritdoc cref="NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.RequireNamedArgumentsTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpRequireNamedArgumentsAnalyzer : RequireNamedArgumentsAnalyzer
    {
    }
}
