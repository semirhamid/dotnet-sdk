// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

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
            context.RegisterCompilationStartAction(context =>
            {
                // Libraries can declare their own copy of the attribute, so there may be more than one type.
                var attributeTypes = context.Compilation.GetTypesByMetadataName(WellKnownTypeNames.SystemDiagnosticsCodeAnalysisRequireNamedArgumentsAttribute);
                if (attributeTypes.IsEmpty)
                {
                    return;
                }

                var expressionType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqExpressionsExpression1);

                // Indexed by parameter ordinal. Empty for a member without affected parameters.
                var affectedParameters = new ConcurrentDictionary<ISymbol, ImmutableArray<bool>>(SymbolEqualityComparer.Default);
                Func<ISymbol, ImmutableArray<bool>> computeAffectedParameters = member => GetAffectedParameters(member, attributeTypes);
                Func<ISymbol, ImmutableArray<bool>> getAffectedParameters = member => affectedParameters.GetOrAdd(member.OriginalDefinition, computeAffectedParameters);

                context.RegisterOperationAction(
                    context => AnalyzeCall(context, getAffectedParameters, expressionType),
                    OperationKind.Invocation,
                    OperationKind.ObjectCreation,
                    OperationKind.PropertyReference);
            });
        }

        /// <summary>
        /// Gets the syntax of an argument that is written without a name, or <see langword="null"/> if the
        /// argument already has a name, or the language has no syntax to name it.
        /// </summary>
        protected abstract SyntaxNode? GetPositionalArgument(IArgumentOperation argument);

        /// <summary>
        /// Gets the name that the language infers from the expression of an argument.
        /// </summary>
        protected abstract string? GetInferredName(SyntaxNode argument);

        internal static (ISymbol? Member, ImmutableArray<IArgumentOperation> Arguments) GetCall(IOperation? operation)
            => operation switch
            {
                IInvocationOperation invocation => (invocation.TargetMethod, invocation.Arguments),
                IObjectCreationOperation creation => (creation.Constructor, creation.Arguments),
                IPropertyReferenceOperation reference => (reference.Property, reference.Arguments),
                _ => (null, ImmutableArray<IArgumentOperation>.Empty),
            };

        private void AnalyzeCall(OperationAnalysisContext context, Func<ISymbol, ImmutableArray<bool>> getAffectedParameters, INamedTypeSymbol? expressionType)
        {
            (ISymbol? member, ImmutableArray<IArgumentOperation> arguments) = GetCall(context.Operation);
            if (member is null || arguments.IsEmpty)
            {
                return;
            }

            var affectedParameters = getAffectedParameters(member);
            if (affectedParameters.IsEmpty)
            {
                return;
            }

            foreach (var argument in arguments)
            {
                // Other kinds are omitted optional arguments and expanded params arrays.
                if (argument is { ArgumentKind: ArgumentKind.Explicit, Parameter: { } parameter }
                    && affectedParameters[parameter.Ordinal]
                    && GetPositionalArgument(argument) is { } syntax
                    && !IsSelfDocumenting(syntax, parameter)
                    && !IsWithinExpressionTree(context.Operation, expressionType))
                {
                    context.ReportDiagnostic(syntax.CreateDiagnostic(Rule, parameter.Name));
                }
            }
        }

        private bool IsSelfDocumenting(SyntaxNode argument, IParameterSymbol parameter)
            => string.Equals(GetInferredName(argument), parameter.Name, StringComparison.OrdinalIgnoreCase);

        private static ImmutableArray<bool> GetAffectedParameters(ISymbol member, ImmutableArray<INamedTypeSymbol> attributeTypes)
        {
            // The attribute cannot be placed on the Invoke method of a delegate.
            bool isWholeMemberAffected = member.HasAnyAttribute(attributeTypes)
                || (member is IMethodSymbol { MethodKind: MethodKind.DelegateInvoke } && member.ContainingType.HasAnyAttribute(attributeTypes));

            var affected = ImmutableArray.CreateRange(member.GetParameters(), p => isWholeMemberAffected || p.HasAnyAttribute(attributeTypes));
            return affected.Contains(true) ? affected : ImmutableArray<bool>.Empty;
        }

        private static bool IsWithinExpressionTree(IOperation operation, INamedTypeSymbol? expressionType)
        {
            // Before C# 14 an expression tree cannot contain named arguments, including in lambdas nested inside it.
            if (expressionType is null)
            {
                return false;
            }

            for (var current = operation; current is not null; current = current.Parent)
            {
                if (current is IAnonymousFunctionOperation
                    && SymbolEqualityComparer.Default.Equals(current.Parent?.Type?.OriginalDefinition, expressionType))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
