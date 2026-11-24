// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers;
using Microsoft.NetCore.Analyzers.Usage;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage
{
    /// <summary>
    /// CA2268: <inheritdoc cref="NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.RequireNamedArgumentsTitle"/>
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpRequireNamedArgumentsFixer : SyntaxEditorBasedCodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(RequireNamedArgumentsAnalyzer.RuleId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SemanticModel model = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            if (TryGetArgumentsToName(root.FindNode(context.Span), model, context.CancellationToken, out _))
            {
                RegisterCodeFix(
                    context,
                    MicrosoftNetCoreAnalyzersResources.RequireNamedArgumentsCodeFixTitle,
                    nameof(MicrosoftNetCoreAnalyzersResources.RequireNamedArgumentsCodeFixTitle));
            }
        }

        protected override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);

            if (TryGetArgumentsToName(node, model, cancellationToken, out ImmutableArray<(SyntaxNode Syntax, string Name)> arguments))
            {
                foreach ((SyntaxNode syntax, string name) in arguments)
                {
                    editor.ReplaceNode(syntax, (current, generator) => AddName(current, name, generator));
                }
            }
        }

        /// <summary>
        /// Gets the argument together with every later argument that has to be named for the call to compile.
        /// </summary>
        private static bool TryGetArgumentsToName(
            SyntaxNode node,
            SemanticModel model,
            CancellationToken cancellationToken,
            out ImmutableArray<(SyntaxNode Syntax, string Name)> arguments)
        {
            arguments = ImmutableArray<(SyntaxNode Syntax, string Name)>.Empty;

            IEnumerable<SyntaxNode> candidates = node.Parent switch
            {
                BaseArgumentListSyntax list => list.Arguments,
                AttributeArgumentListSyntax list => list.Arguments,
                _ => Enumerable.Empty<SyntaxNode>(),
            };

            // A positional argument can follow a named one only since C# 7.2.
            candidates = candidates.SkipWhile(candidate => candidate != node);
            if (((CSharpParseOptions)node.SyntaxTree.Options).LanguageVersion >= LanguageVersion.CSharp7_2)
            {
                candidates = candidates.Take(1);
            }

            IOperation? operation = node.Parent?.Parent is { } callSyntax ? model.GetOperation(callSyntax, cancellationToken) : null;
            var call = RequireNamedArgumentsAnalyzer.GetCall(operation is IAttributeOperation attribute ? attribute.Operation : operation);

            var builder = ImmutableArray.CreateBuilder<(SyntaxNode Syntax, string Name)>();
            foreach (SyntaxNode candidate in candidates)
            {
                if (CSharpRequireNamedArgumentsAnalyzer.IsNamed(candidate))
                {
                    continue;
                }

                // An argument of an expanded params array has no argument operation of its own, and cannot be named.
                if (call.Arguments.FirstOrDefault(a => CSharpRequireNamedArgumentsAnalyzer.GetArgumentSyntax(a) == candidate)?.Parameter is not { } parameter)
                {
                    return false;
                }

                builder.Add((candidate, parameter.Name));
            }

            arguments = builder.ToImmutable();
            return !arguments.IsEmpty;
        }

        private static SyntaxNode AddName(SyntaxNode argument, string name, SyntaxGenerator generator)
        {
            // The fix for an earlier argument of the same call may have named this one already.
            if (CSharpRequireNamedArgumentsAnalyzer.IsNamed(argument))
            {
                return argument;
            }

            NameColonSyntax nameColon = SyntaxFactory.NameColon((IdentifierNameSyntax)generator.IdentifierName(name))
                .WithTrailingTrivia(SyntaxFactory.Space);

            SyntaxNode withoutLeadingTrivia = argument.WithoutLeadingTrivia();
            SyntaxNode named = withoutLeadingTrivia switch
            {
                ArgumentSyntax syntax => syntax.WithNameColon(nameColon),
                AttributeArgumentSyntax syntax => syntax.WithNameColon(nameColon),
                _ => withoutLeadingTrivia,
            };

            return named.WithLeadingTrivia(argument.GetLeadingTrivia());
        }
    }
}
