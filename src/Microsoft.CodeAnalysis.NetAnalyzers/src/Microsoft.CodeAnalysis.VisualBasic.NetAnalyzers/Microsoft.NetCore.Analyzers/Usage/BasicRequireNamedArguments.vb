' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Usage

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Usage
    ''' <summary>
    ''' CA2268: <inheritdoc cref="NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.RequireNamedArgumentsTitle"/>
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicRequireNamedArgumentsAnalyzer
        Inherits RequireNamedArgumentsAnalyzer

        Protected Overrides Function GetPositionalArgument(argument As IArgumentOperation) As SyntaxNode
            ' A named argument cannot match a ParamArray parameter, and the constructor arguments of an attribute
            ' cannot be named.
            Dim syntax = TryCast(argument.Syntax, SimpleArgumentSyntax)
            If syntax Is Nothing OrElse
                syntax.IsNamed OrElse
                argument.Parameter.IsParams OrElse
                TypeOf syntax.Parent.Parent Is AttributeSyntax Then

                Return Nothing
            End If

            Return syntax
        End Function

        Protected Overrides Function GetInferredName(argument As SyntaxNode) As String
            Return DirectCast(argument, SimpleArgumentSyntax).Expression.TryGetInferredMemberName()
        End Function
    End Class
End Namespace
