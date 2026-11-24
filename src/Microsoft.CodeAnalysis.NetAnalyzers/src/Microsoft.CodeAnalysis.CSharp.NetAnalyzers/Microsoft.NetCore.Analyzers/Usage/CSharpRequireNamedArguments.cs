// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Usage;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage
{
    /// <summary>
    /// CA2268: <inheritdoc cref="NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.RequireNamedArgumentsTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpRequireNamedArgumentsAnalyzer : RequireNamedArgumentsAnalyzer
    {
        protected override SyntaxNode? GetPositionalArgument(IArgumentOperation argument)
            => GetArgumentSyntax(argument) is { } syntax && !IsNamed(syntax) ? syntax : null;

        protected override string? GetInferredName(SyntaxNode argument)
            => GetExpression(argument) switch
            {
                // out var name
                DeclarationExpressionSyntax declaration => declaration.Designation.TryGetInferredMemberName(),
                { } expression => expression.TryGetInferredMemberName(),
                null => null,
            };

        /// <summary>
        /// Gets the <see cref="ArgumentSyntax"/> or <see cref="AttributeArgumentSyntax"/> that the caller wrote for
        /// an argument, or <see langword="null"/> if it has none, such as the receiver of an extension method.
        /// </summary>
        internal static SyntaxNode? GetArgumentSyntax(IArgumentOperation argument)
        {
            SyntaxNode? syntax = argument.Syntax;

            if (syntax is ExpressionSyntax)
            {
                // These expressions have no operation, so the argument has the syntax of the expression inside them.
                while (syntax?.Parent?.Kind() is SyntaxKind.ParenthesizedExpression
                    or SyntaxKind.CheckedExpression
                    or SyntaxKind.UncheckedExpression
                    or SyntaxKind.SuppressNullableWarningExpression)
                {
                    syntax = syntax.Parent;
                }

                syntax = syntax?.Parent;
            }

            // The implicit constructor call of an interpolated string handler has the syntax of the interpolated
            // string, which can itself be an argument of another call.
            return syntax is ArgumentSyntax or AttributeArgumentSyntax && syntax.Parent?.Parent == argument.Parent?.Syntax
                ? syntax
                : null;
        }

        internal static bool IsNamed(SyntaxNode argument)
            => argument is ArgumentSyntax { NameColon: not null }
                or AttributeArgumentSyntax { NameColon: not null }
                or AttributeArgumentSyntax { NameEquals: not null };

        private static ExpressionSyntax? GetExpression(SyntaxNode argument)
            => argument switch
            {
                ArgumentSyntax syntax => syntax.Expression,
                AttributeArgumentSyntax syntax => syntax.Expression,
                _ => null,
            };
    }
}
