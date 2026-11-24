' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.NetCore.Analyzers.Usage

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Usage
    ''' <summary>
    ''' CA2268: <inheritdoc cref="NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.RequireNamedArgumentsTitle"/>
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicRequireNamedArgumentsAnalyzer
        Inherits RequireNamedArgumentsAnalyzer
    End Class
End Namespace
