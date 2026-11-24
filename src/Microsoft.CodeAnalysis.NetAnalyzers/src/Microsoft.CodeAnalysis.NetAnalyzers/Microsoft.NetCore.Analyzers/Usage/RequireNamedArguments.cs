// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Usage
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2268: <inheritdoc cref="RequireNamedArgumentsTitle"/>
    /// </summary>
    public abstract class RequireNamedArgumentsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2268";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(RequireNamedArgumentsTitle)),
            CreateLocalizableResourceString(nameof(RequireNamedArgumentsMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(RequireNamedArgumentsDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        }
    }
}
