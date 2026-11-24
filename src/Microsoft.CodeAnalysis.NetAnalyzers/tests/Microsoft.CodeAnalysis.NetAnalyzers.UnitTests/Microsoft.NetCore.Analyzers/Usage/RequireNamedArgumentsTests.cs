// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Usage.CSharpRequireNamedArgumentsAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Usage.CSharpRequireNamedArgumentsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Usage.BasicRequireNamedArgumentsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Usage.UnitTests
{
    [TestClass]
    public sealed class RequireNamedArgumentsTests
    {
        private const string AttributeSourceCS = """
            namespace System.Diagnostics.CodeAnalysis
            {
                [AttributeUsage(
                    AttributeTargets.Method |
                    AttributeTargets.Constructor |
                    AttributeTargets.Property |
                    AttributeTargets.Delegate |
                    AttributeTargets.Parameter,
                    AllowMultiple = false,
                    Inherited = false)]
                public sealed class RequireNamedArgumentsAttribute : Attribute
                {
                }
            }
            """;

        private const string AttributeSourceVB = """
            Namespace System.Diagnostics.CodeAnalysis
                <AttributeUsage(
                    AttributeTargets.Method Or
                    AttributeTargets.Constructor Or
                    AttributeTargets.Property Or
                    AttributeTargets.Delegate Or
                    AttributeTargets.Parameter,
                    AllowMultiple:=False,
                    Inherited:=False)>
                Public NotInheritable Class RequireNamedArgumentsAttribute
                    Inherits Attribute
                End Class
            End Namespace
            """;

        [TestMethod]
        public async Task Method_AllParametersAffected_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    void Instance(int first, int second) { }

                    [RequireNamedArguments]
                    static int Static(int first) => first;

                    [RequireNamedArguments]
                    T Generic<T>(T first) => first;

                    void NotAttributed(int first, int second) { }

                    void Test()
                    {
                        Instance([|1|], [|2|]);
                        this.Instance([|3|], [|4|]);
                        C.Static([|5|]);
                        Generic([|""|]);
                        NotAttributed(1, 2);
                    }
                }
                """,
                """
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    void Instance(int first, int second) { }

                    [RequireNamedArguments]
                    static int Static(int first) => first;

                    [RequireNamedArguments]
                    T Generic<T>(T first) => first;

                    void NotAttributed(int first, int second) { }

                    void Test()
                    {
                        Instance(first: 1, second: 2);
                        this.Instance(first: 3, second: 4);
                        C.Static(first: 5);
                        Generic(first: "");
                        NotAttributed(1, 2);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task LocalFunction_Diagnostic_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    void Test()
                    {
                        Local([|1|]);

                        [RequireNamedArguments]
                        void Local(int count) { }
                    }
                }
                """,
                """
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    void Test()
                    {
                        Local(count: 1);

                        [RequireNamedArguments]
                        void Local(int count) { }
                    }
                }
                """,
                LanguageVersion.CSharp9);
        }

        [TestMethod]
        public async Task Constructor_Diagnostic_CS()
        {
            await VerifyCSAsync("""
                using System;
                using System.Diagnostics.CodeAnalysis;

                class B
                {
                    [RequireNamedArguments]
                    public B(int x, int y) { }

                    public B() : this([|1|], [|2|]) { }
                }

                class D : B
                {
                    public D() : base([|1|], [|2|]) { }

                    B Create() => new B([|1|], [|2|]) { };

                    B CreateTargetTyped() => new([|1|], [|2|]);
                }

                struct S
                {
                    [RequireNamedArguments]
                    public S(int x) { }

                    static S Create() => new S([|1|]);
                }

                class MyAttribute : Attribute
                {
                    [RequireNamedArguments]
                    public MyAttribute(int x) { }

                    public int Y { get; set; }
                }

                [My([|1|], Y = 2)]
                class Attributed { }
                """,
                """
                using System;
                using System.Diagnostics.CodeAnalysis;

                class B
                {
                    [RequireNamedArguments]
                    public B(int x, int y) { }

                    public B() : this(x: 1, y: 2) { }
                }

                class D : B
                {
                    public D() : base(x: 1, y: 2) { }

                    B Create() => new B(x: 1, y: 2) { };

                    B CreateTargetTyped() => new(x: 1, y: 2);
                }

                struct S
                {
                    [RequireNamedArguments]
                    public S(int x) { }

                    static S Create() => new S(x: 1);
                }

                class MyAttribute : Attribute
                {
                    [RequireNamedArguments]
                    public MyAttribute(int x) { }

                    public int Y { get; set; }
                }

                [My(x: 1, Y = 2)]
                class Attributed { }
                """,
                LanguageVersion.CSharp9);
        }

        [TestMethod]
        public async Task PrimaryConstructor_Diagnostic_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                [method: RequireNamedArguments]
                class B(int x)
                {
                    public int X => x;
                }

                class D(int x, int other) : B([|other|])
                {
                    public int Sum => x + other;
                }

                class E(int x) : B(x);
                """,
                """
                using System.Diagnostics.CodeAnalysis;

                [method: RequireNamedArguments]
                class B(int x)
                {
                    public int X => x;
                }

                class D(int x, int other) : B(x: other)
                {
                    public int Sum => x + other;
                }

                class E(int x) : B(x);
                """,
                LanguageVersion.CSharp12);
        }

        [TestMethod]
        public async Task Indexer_Diagnostic_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    public int this[int row, int column]
                    {
                        get => 0;
                        set { }
                    }

                    static void Test(C c, int row, int column)
                    {
                        _ = c[[|1|], [|2|]];
                        c[[|3|], [|4|]] = 5;
                        c[[|6|], [|7|]] += 8;
                        _ = c?[[|9|], column];
                        c[row, column] = 10;
                        _ = new C { [[|11|], [|12|]] = 13 };
                    }
                }
                """,
                """
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    public int this[int row, int column]
                    {
                        get => 0;
                        set { }
                    }

                    static void Test(C c, int row, int column)
                    {
                        _ = c[row: 1, column: 2];
                        c[row: 3, column: 4] = 5;
                        c[row: 6, column: 7] += 8;
                        _ = c?[row: 9, column];
                        c[row, column] = 10;
                        _ = new C { [row: 11, column: 12] = 13 };
                    }
                }
                """);
        }

        [TestMethod]
        public async Task IndexerAccessorAttribute_NoDiagnostic_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    public int this[int index]
                    {
                        [RequireNamedArguments] get => 0;
                        [RequireNamedArguments] set { }
                    }

                    [RequireNamedArguments]
                    public int Property { get; set; }

                    static void Test(C c)
                    {
                        _ = c[1];
                        c[2] = 3;
                        c.Property = 4;
                    }
                }
                """);
        }

        [TestMethod]
        public async Task Delegate_Diagnostic_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                [RequireNamedArguments]
                delegate void Attributed(int count, int other);

                [RequireNamedArguments]
                delegate T AttributedGeneric<T>(T count);

                delegate void NotAttributed(int count);

                delegate void AttributedParameter(int count, [RequireNamedArguments] int other);

                class C
                {
                    event Attributed Event;

                    void Test(Attributed a, AttributedGeneric<string> g, NotAttributed n, AttributedParameter p, int count)
                    {
                        a([|1|], [|2|]);
                        a.Invoke([|3|], [|4|]);
                        a?.Invoke([|5|], [|6|]);
                        Event(count, [|7|]);
                        g([|""|]);
                        n(1);
                        p(8, [|9|]);
                        a.BeginInvoke(1, 2, null, null);
                    }
                }
                """,
                """
                using System.Diagnostics.CodeAnalysis;

                [RequireNamedArguments]
                delegate void Attributed(int count, int other);

                [RequireNamedArguments]
                delegate T AttributedGeneric<T>(T count);

                delegate void NotAttributed(int count);

                delegate void AttributedParameter(int count, [RequireNamedArguments] int other);

                class C
                {
                    event Attributed Event;

                    void Test(Attributed a, AttributedGeneric<string> g, NotAttributed n, AttributedParameter p, int count)
                    {
                        a(count: 1, other: 2);
                        a.Invoke(count: 3, other: 4);
                        a?.Invoke(count: 5, other: 6);
                        Event(count, other: 7);
                        g(count: "");
                        n(1);
                        p(8, other: 9);
                        a.BeginInvoke(1, 2, null, null);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task ParameterLevel_OtherParametersUnaffected_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    C(int first, [RequireNamedArguments] int second) { }

                    int this[int first, [RequireNamedArguments] int second] => 0;

                    static void Log(string message, [RequireNamedArguments] string category, int level) { }

                    static void Test(C c)
                    {
                        Log("text", [|"Networking"|], 1);
                        Log("text", category: "Networking", 1);
                        _ = new C(1, [|2|]);
                        _ = c[1, [|2|]];
                    }
                }
                """,
                """
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    C(int first, [RequireNamedArguments] int second) { }

                    int this[int first, [RequireNamedArguments] int second] => 0;

                    static void Log(string message, [RequireNamedArguments] string category, int level) { }

                    static void Test(C c)
                    {
                        Log("text", category: "Networking", 1);
                        Log("text", category: "Networking", 1);
                        _ = new C(1, second: 2);
                        _ = c[1, second: 2];
                    }
                }
                """);
        }

        [TestMethod]
        // Literals and non-trivial expressions
        [DataRow("42")]
        [DataRow("-count")]
        [DataRow("count + 1")]
        [DataRow("GetCount()")]
        [DataRow("(count)")]
        [DataRow("((42))")]
        [DataRow("checked(count + 1)")]
        [DataRow("unchecked((value))")]
        [DataRow("(int)count")]
        [DataRow("values[0]")]
        [DataRow("default(int)")]
        [DataRow("nullable.Value")]
        // Identifiers with a different name
        [DataRow("value")]
        [DataRow("_count")]
        [DataRow("this.value")]
        public async Task ExpressionIsNotSelfDocumenting_Diagnostic_CS(string expression)
        {
            await VerifyCSAsync($$"""
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    int _count = 0, value = 0;
                    int[] values = null;
                    int? nullable = 0;

                    static int GetCount() => 0;

                    [RequireNamedArguments]
                    void M(int count) { }

                    void Test(int count)
                    {
                        M([|{{expression}}|]);
                    }
                }
                """,
                $$"""
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    int _count = 0, value = 0;
                    int[] values = null;
                    int? nullable = 0;

                    static int GetCount() => 0;

                    [RequireNamedArguments]
                    void M(int count) { }

                    void Test(int count)
                    {
                        M(count: {{expression}});
                    }
                }
                """);
        }

        [TestMethod]
        [DataRow("count")]
        [DataRow("@count")]
        [DataRow("Count")]
        [DataRow("this.Count")]
        [DataRow("other.Count")]
        [DataRow("other?.Count")]
        [DataRow("count: 42")]
        [DataRow("count: value")]
        public async Task ExpressionIsSelfDocumentingOrNamed_NoDiagnostic_CS(string argument)
        {
            await VerifyCSAsync($$"""
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    int Count = 0, value = 0;
                    C other = null;

                    [RequireNamedArguments]
                    void M(int? count) { }

                    void Test(int count)
                    {
                        M({{argument}});
                    }
                }
                """);
        }

        [TestMethod]
        public async Task NameDiffersOnlyByCase_NoDiagnostic_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                class R
                {
                    [RequireNamedArguments]
                    public R(int Value) { }

                    static R Create(int value) => new R(value);
                }
                """);
        }

        [TestMethod]
        public async Task SuppressedExpression_Diagnostic_CS()
        {
            await VerifyCSAsync("""
                #nullable enable
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static void M(string name) { }

                    static void Test(string? name) => M([|name!|]);
                }
                """,
                """
                #nullable enable
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static void M(string name) { }

                    static void Test(string? name) => M(name: name!);
                }
                """,
                LanguageVersion.CSharp8);
        }

        [TestMethod]
        public async Task RefKinds_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static void M(ref int first, out int second, in int third) { second = 0; }

                    static void Test(int a, int b, int first, int third)
                    {
                        M([|ref a|], [|out b|], [|in a|]);
                        M([|ref a|], [|out var c|], [|1|]);
                        M([|ref a|], [|out _|], [|a|]);
                        M(ref first, out int second, in third);
                    }
                }
                """,
                """
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static void M(ref int first, out int second, in int third) { second = 0; }

                    static void Test(int a, int b, int first, int third)
                    {
                        M(first: ref a, second: out b, third: in a);
                        M(first: ref a, second: out var c, third: 1);
                        M(first: ref a, second: out _, third: a);
                        M(ref first, out int second, in third);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task MixedNamedAndPositional_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static void M(int first, int second, int third) { }

                    static void Test(int second)
                    {
                        M([|1|], second: 2, third: 3);
                        M(first: 1, [|2|], third: 3);
                        M([|1|], third: 3, second: 2);
                        M([|1|], second, [|3|]);
                        M(third: 3, second: 2, first: 1);
                    }
                }
                """,
                """
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static void M(int first, int second, int third) { }

                    static void Test(int second)
                    {
                        M(first: 1, second: 2, third: 3);
                        M(first: 1, second: 2, third: 3);
                        M(first: 1, third: 3, second: 2);
                        M(first: 1, second, third: 3);
                        M(third: 3, second: 2, first: 1);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task OmittedOptionalAndParameterless_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;
                using System.Runtime.CompilerServices;

                class C
                {
                    [RequireNamedArguments]
                    static void Optional(int x = 1, int y = 2, [CallerMemberName] string caller = null) { }

                    [RequireNamedArguments]
                    static void Parameterless() { }

                    static void Test()
                    {
                        Parameterless();
                        Optional();
                        Optional([|3|]);
                    }
                }
                """,
                """
                using System.Diagnostics.CodeAnalysis;
                using System.Runtime.CompilerServices;

                class C
                {
                    [RequireNamedArguments]
                    static void Optional(int x = 1, int y = 2, [CallerMemberName] string caller = null) { }

                    [RequireNamedArguments]
                    static void Parameterless() { }

                    static void Test()
                    {
                        Parameterless();
                        Optional();
                        Optional(x: 3);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task ParamsArray_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static void M(int first, params int[] rest) { }

                    static void Test(int first, int[] rest)
                    {
                        M(first);
                        M(first, 2);
                        M([|1|], 2, 3);
                        M(first, [|new[] { 2, 3 }|]);
                        M(first, rest);
                        M(first, rest: new[] { 2, 3 });
                    }
                }
                """,
                """
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static void M(int first, params int[] rest) { }

                    static void Test(int first, int[] rest)
                    {
                        M(first);
                        M(first, 2);
                        M(first: 1, 2, 3);
                        M(first, rest: new[] { 2, 3 });
                        M(first, rest);
                        M(first, rest: new[] { 2, 3 });
                    }
                }
                """);
        }

        [TestMethod]
        public async Task ParamsCollection_CS()
        {
            await VerifyCSAsync("""
                using System.Collections.Generic;
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static void M(params List<int> rest) { }

                    static void Test()
                    {
                        M();
                        M(1, 2);
                        M([|new List<int>()|]);
                    }
                }
                """,
                """
                using System.Collections.Generic;
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static void M(params List<int> rest) { }

                    static void Test()
                    {
                        M();
                        M(1, 2);
                        M(rest: new List<int>());
                    }
                }
                """,
                LanguageVersion.CSharp13);
        }

        [TestMethod]
        public async Task ExtensionMethod_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                static class Extensions
                {
                    [RequireNamedArguments]
                    public static int AddTo(this int self, int other) => self + other;

                    [RequireNamedArguments]
                    public static int Negate(this int self) => -self;

                    public static int OnlyReceiver([RequireNamedArguments] this int self, int other) => self + other;

                    static void Test(int value, int self)
                    {
                        value.AddTo([|1|]);
                        1.AddTo([|2|]).AddTo(other: 3);
                        (value).AddTo([|(1)|]);
                        value.Negate();
                        value.OnlyReceiver(1);
                        Extensions.AddTo([|value|], [|1|]);
                        Extensions.AddTo(self, [|1|]);
                    }
                }
                """,
                """
                using System.Diagnostics.CodeAnalysis;

                static class Extensions
                {
                    [RequireNamedArguments]
                    public static int AddTo(this int self, int other) => self + other;

                    [RequireNamedArguments]
                    public static int Negate(this int self) => -self;

                    public static int OnlyReceiver([RequireNamedArguments] this int self, int other) => self + other;

                    static void Test(int value, int self)
                    {
                        value.AddTo(other: 1);
                        1.AddTo(other: 2).AddTo(other: 3);
                        (value).AddTo(other: (1));
                        value.Negate();
                        value.OnlyReceiver(1);
                        Extensions.AddTo(self: value, other: 1);
                        Extensions.AddTo(self, other: 1);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task ExtensionBlock_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                static class Extensions
                {
                    extension(int self)
                    {
                        [RequireNamedArguments]
                        public int AddTo(int other) => self + other;

                        [RequireNamedArguments]
                        public static int Create(int count) => count;
                    }

                    static void Test(int value)
                    {
                        value.AddTo([|1|]);
                        (value).AddTo(other: 1);
                        int.Create([|1|]);
                        Extensions.AddTo([|value|], [|1|]);
                    }
                }
                """,
                """
                using System.Diagnostics.CodeAnalysis;

                static class Extensions
                {
                    extension(int self)
                    {
                        [RequireNamedArguments]
                        public int AddTo(int other) => self + other;

                        [RequireNamedArguments]
                        public static int Create(int count) => count;
                    }

                    static void Test(int value)
                    {
                        value.AddTo(other: 1);
                        (value).AddTo(other: 1);
                        int.Create(count: 1);
                        Extensions.AddTo(self: value, other: 1);
                    }
                }
                """,
                LanguageVersion.Preview);
        }

        [TestMethod]
        public async Task OverrideAndInterface_OnlyBoundDeclarationCounts_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                interface IOnInterface
                {
                    [RequireNamedArguments]
                    void M(int slot);
                }

                class OnInterface : IOnInterface
                {
                    public void M(int impl) { }
                }

                interface IOnImplementation
                {
                    void M(int slot);
                }

                class OnImplementation : IOnImplementation
                {
                    [RequireNamedArguments]
                    public void M(int impl) { }
                }

                class Base
                {
                    [RequireNamedArguments]
                    public virtual void OnBase(int baseName) { }

                    public virtual void OnDerived(int baseName) { }
                }

                class Derived : Base
                {
                    public override void OnBase(int derivedName) { }

                    [RequireNamedArguments]
                    public override void OnDerived(int derivedName) { }
                }

                class C
                {
                    void Test(IOnInterface i1, OnInterface c1, IOnImplementation i2, OnImplementation c2, Base b, Derived d)
                    {
                        i1.M([|1|]);
                        c1.M(1);
                        i2.M(1);
                        c2.M([|1|]);
                        b.OnBase([|1|]);
                        d.OnBase(1);
                        b.OnDerived(1);
                        d.OnDerived([|1|]);
                    }
                }
                """,
                """
                using System.Diagnostics.CodeAnalysis;

                interface IOnInterface
                {
                    [RequireNamedArguments]
                    void M(int slot);
                }

                class OnInterface : IOnInterface
                {
                    public void M(int impl) { }
                }

                interface IOnImplementation
                {
                    void M(int slot);
                }

                class OnImplementation : IOnImplementation
                {
                    [RequireNamedArguments]
                    public void M(int impl) { }
                }

                class Base
                {
                    [RequireNamedArguments]
                    public virtual void OnBase(int baseName) { }

                    public virtual void OnDerived(int baseName) { }
                }

                class Derived : Base
                {
                    public override void OnBase(int derivedName) { }

                    [RequireNamedArguments]
                    public override void OnDerived(int derivedName) { }
                }

                class C
                {
                    void Test(IOnInterface i1, OnInterface c1, IOnImplementation i2, OnImplementation c2, Base b, Derived d)
                    {
                        i1.M(slot: 1);
                        c1.M(1);
                        i2.M(1);
                        c2.M(impl: 1);
                        b.OnBase(baseName: 1);
                        d.OnBase(1);
                        b.OnDerived(1);
                        d.OnDerived(derivedName: 1);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task Overload_OnlyAttributedOverloadReports_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    static void M(int x, int y) { }

                    [RequireNamedArguments]
                    static void M(string x, string y) { }

                    static void Test()
                    {
                        M(1, 2);
                        M([|"a"|], [|"b"|]);
                    }
                }
                """,
                """
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    static void M(int x, int y) { }

                    [RequireNamedArguments]
                    static void M(string x, string y) { }

                    static void Test()
                    {
                        M(1, 2);
                        M(x: "a", y: "b");
                    }
                }
                """);
        }

        [TestMethod]
        public async Task MethodGroup_NoDiagnostic_CS()
        {
            await VerifyCSAsync("""
                using System;
                using System.Collections.Generic;
                using System.Diagnostics.CodeAnalysis;
                using System.Linq;

                class C
                {
                    [RequireNamedArguments]
                    static int M(int count) => count;

                    static void Test(IEnumerable<int> values)
                    {
                        Func<int, int> f = M;
                        var g = new Func<int, int>(M);
                        f(1);
                        _ = values.Select(M);
                        _ = nameof(M);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task NamedSyntaxUnavailable_NoDiagnostic_CS()
        {
            await VerifyCSAsync("""
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Diagnostics.CodeAnalysis;
                using System.Linq.Expressions;

                class Collection : IEnumerable
                {
                    [RequireNamedArguments]
                    public void Add(int first, int second) { }

                    [RequireNamedArguments]
                    public static int operator +(Collection left, int right) => right;

                    [RequireNamedArguments]
                    public static implicit operator Collection(int value) => new Collection();

                    IEnumerator IEnumerable.GetEnumerator() => null;
                }

                class Query
                {
                    [RequireNamedArguments]
                    public Query Select(Func<int, int> selector) => this;
                }

                class C
                {
                    [RequireNamedArguments]
                    static int M(int count) => count;

                    static void Test(dynamic d)
                    {
                        _ = new Collection { { 1, 2 }, { 3, 4 } };
                        _ = new Collection() + 1;
                        Collection converted = 1;
                        _ = from x in new Query() select x + 1;
                        M(d);
                        Expression<Func<int>> direct = () => M(1);
                        Expression<Func<Func<int>>> nested = () => () => M(1);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task LambdaInsideMethodTakingExpression_OnlyTreeIsSkipped_CS()
        {
            await VerifyCSAsync("""
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Linq.Expressions;

                class C
                {
                    [RequireNamedArguments]
                    static int M(int count) => count;

                    static void Tree(Expression<Func<int>> e, int x) { }

                    static void Test()
                    {
                        Tree(() => M(1), M([|2|]));
                        Func<int> f = () => M([|3|]);
                    }
                }
                """,
                """
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Linq.Expressions;

                class C
                {
                    [RequireNamedArguments]
                    static int M(int count) => count;

                    static void Tree(Expression<Func<int>> e, int x) { }

                    static void Test()
                    {
                        Tree(() => M(1), M(count: 2));
                        Func<int> f = () => M(count: 3);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task InterpolatedStringHandler_OnlyWrittenArgumentsReport_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;
                using System.Runtime.CompilerServices;

                [InterpolatedStringHandler]
                struct Handler
                {
                    [RequireNamedArguments]
                    public Handler(int literalLength, int formattedCount) { }

                    [RequireNamedArguments]
                    public void AppendLiteral(string s) { }

                    [RequireNamedArguments]
                    public void AppendFormatted<T>(T t, int alignment = 0) { }
                }

                class C
                {
                    static void NotAttributed(Handler handler) { }

                    [RequireNamedArguments]
                    static void Attributed(Handler handler) { }

                    static void Test(int x)
                    {
                        NotAttributed($"a{x,1}");
                        Attributed([|$"a{x,1}"|]);
                        Attributed([|($"a{x,1}")|]);
                    }
                }
                """,
                """
                using System.Diagnostics.CodeAnalysis;
                using System.Runtime.CompilerServices;

                [InterpolatedStringHandler]
                struct Handler
                {
                    [RequireNamedArguments]
                    public Handler(int literalLength, int formattedCount) { }

                    [RequireNamedArguments]
                    public void AppendLiteral(string s) { }

                    [RequireNamedArguments]
                    public void AppendFormatted<T>(T t, int alignment = 0) { }
                }

                class C
                {
                    static void NotAttributed(Handler handler) { }

                    [RequireNamedArguments]
                    static void Attributed(Handler handler) { }

                    static void Test(int x)
                    {
                        NotAttributed($"a{x,1}");
                        Attributed(handler: $"a{x,1}");
                        Attributed(handler: ($"a{x,1}"));
                    }
                }
                """,
                LanguageVersion.CSharp10,
                ReferenceAssemblies.Net.Net80);
        }

        [TestMethod]
        public async Task ImplicitIndexSupport_NoDiagnostic_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    public int Length => 1;

                    [RequireNamedArguments]
                    public int this[int index] => 0;

                    [RequireNamedArguments]
                    public int Slice(int start, int length) => 0;

                    static void Test(C c)
                    {
                        _ = c[^1];
                        _ = c[0..1];
                    }
                }
                """,
                languageVersion: LanguageVersion.CSharp8,
                referenceAssemblies: ReferenceAssemblies.Net.Net80);
        }

        [TestMethod]
        public async Task KeywordParameterName_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static void M(int @class) { }

                    static void Test()
                    {
                        int @class = 1;
                        M([|1|]);
                        M(@class);
                    }
                }
                """,
                """
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static void M(int @class) { }

                    static void Test()
                    {
                        int @class = 1;
                        M(@class: 1);
                        M(@class);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task Trivia_IsPreserved_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static void M(int first, ref int second) { }

                    static void Test(int x)
                    {
                        M(/* a */ [|1|] /* b */, /* c */ [|ref x|] /* d */);
                        M(
                            // first
                            [|1|],

                            // second
                            [|ref x|]); // end
                    }
                }
                """,
                """
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static void M(int first, ref int second) { }

                    static void Test(int x)
                    {
                        M(/* a */ first: 1 /* b */, /* c */ second: ref x /* d */);
                        M(
                            // first
                            first: 1,

                            // second
                            second: ref x); // end
                    }
                }
                """);
        }

        [TestMethod]
        public async Task NestedCalls_FixAll_CS()
        {
            await VerifyCSAsync("""
                using System;
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static int Add(int left, int right) => left + right;

                    static void Run(Func<int> func) { }

                    static void Test()
                    {
                        Add([|Add([|1|], [|2|])|], [|Add([|3|], [|Add([|4|], [|5|])|])|]);
                        Run(() => Add([|6|], [|7|]));
                    }
                }
                """,
                """
                using System;
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static int Add(int left, int right) => left + right;

                    static void Run(Func<int> func) { }

                    static void Test()
                    {
                        Add(left: Add(left: 1, right: 2), right: Add(left: 3, right: Add(left: 4, right: 5)));
                        Run(() => Add(left: 6, right: 7));
                    }
                }
                """);
        }

        [TestMethod]
        public async Task NonTrailingNamedArgumentsUnsupported_LaterArgumentsAreNamed_CS()
        {
            await VerifyCSAsync("""
                using System;
                using System.Diagnostics.CodeAnalysis;

                class MyAttribute : Attribute
                {
                    public MyAttribute([RequireNamedArguments] int first, int second) { }

                    public int Third { get; set; }
                }

                [My([|1|], 2, Third = 3)]
                class C
                {
                    int this[[RequireNamedArguments] int first, int second] => 0;

                    static int M(int first, [RequireNamedArguments] int second, int third, int fourth = 4) => 0;

                    static void Test(C c, int third)
                    {
                        M(1, [|2|], 3);
                        M(1, [|2|], third, fourth: 4);
                        M(1, [|(2)|], (3));
                        M(1, [|M(1, [|2|], 3)|], M(4, [|5|], 6));
                        _ = c[[|1|], 2];
                    }
                }
                """,
                """
                using System;
                using System.Diagnostics.CodeAnalysis;

                class MyAttribute : Attribute
                {
                    public MyAttribute([RequireNamedArguments] int first, int second) { }

                    public int Third { get; set; }
                }

                [My(first: 1, second: 2, Third = 3)]
                class C
                {
                    int this[[RequireNamedArguments] int first, int second] => 0;

                    static int M(int first, [RequireNamedArguments] int second, int third, int fourth = 4) => 0;

                    static void Test(C c, int third)
                    {
                        M(1, second: 2, third: 3);
                        M(1, second: 2, third: third, fourth: 4);
                        M(1, second: (2), third: (3));
                        M(1, second: M(1, second: 2, third: 3), third: M(4, second: 5, third: 6));
                        _ = c[first: 1, second: 2];
                    }
                }
                """,
                LanguageVersion.CSharp7_1);
        }

        [TestMethod]
        public async Task NonTrailingNamedArgumentsUnsupported_AllArgumentsReported_CS()
        {
            await VerifyCSAsync("""
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static int Add(int left, int right) => left + right;

                    static void Test()
                    {
                        Add([|1|], [|Add([|2|], [|3|])|]);
                    }
                }
                """,
                """
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static int Add(int left, int right) => left + right;

                    static void Test()
                    {
                        Add(left: 1, right: Add(left: 2, right: 3));
                    }
                }
                """,
                LanguageVersion.CSharp7_1);
        }

        [TestMethod]
        public async Task NonTrailingNamedArgumentsUnsupported_ExpandedParams_NoFix_CS()
        {
            string source = """
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static void M(int first, params int[] rest) { }

                    static void Test()
                    {
                        M([|1|], 2);
                        M([|1|], 2, 3);
                    }
                }
                """;

            await VerifyCSAsync(source, source, LanguageVersion.CSharp7_1);
        }

        [TestMethod]
        public async Task MessageContainsParameterName_CS()
        {
            var test = CreateCSTest("""
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static void M(int first, int second) { }

                    static void Test() => M({|#0:1|}, {|#1:2|});
                }
                """,
                """
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static void M(int first, int second) { }

                    static void Test() => M(first: 1, second: 2);
                }
                """);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic().WithLocation(0).WithArguments("first").WithSeverity(DiagnosticSeverity.Info));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic().WithLocation(1).WithArguments("second").WithSeverity(DiagnosticSeverity.Info));
            await test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task ConfiguredSeverity_IsRespected_CS()
        {
            var test = CreateCSTest("""
                using System.Diagnostics.CodeAnalysis;

                class C
                {
                    [RequireNamedArguments]
                    static void M(int count) { }

                    static void Test() => M(1);
                }
                """);
            test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", """
                is_global = true
                dotnet_diagnostic.CA2268.severity = none
                """));
            await test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task AttributeTypeMissing_NoDiagnostic_CS()
        {
            string source = """
                class C
                {
                    static void M(int count) { }

                    static void Test() => M(1);
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
        [DataRow("System.Runtime.CompilerServices", "RequireNamedArgumentsAttribute")]
        [DataRow("System.Diagnostics.CodeAnalysis", "RequireNamedArgumentAttribute")]
        [DataRow("MyApp", "RequireNamedArgumentsAttribute")]
        public async Task AttributeWithOtherNameOrNamespace_NoDiagnostic_CS(string @namespace, string name)
        {
            string source = $$"""
                namespace {{@namespace}}
                {
                    class {{name}} : System.Attribute { }
                }

                class C
                {
                    [{{@namespace}}.{{name}}]
                    static void M(int count) { }

                    static void Test() => M(1);
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
        [DataRow("public")]
        [DataRow("internal")]
        public async Task AttributeInReferencedAssembly_Diagnostic_CS(string accessibility)
        {
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        """
                        class C
                        {
                            static void Test(int count)
                            {
                                Library.M([|1|]);
                                Library.M(count);
                            }
                        }
                        """,
                    },
                    AdditionalProjectReferences = { "Library" },
                    AdditionalProjects =
                    {
                        ["Library"] =
                        {
                            Sources =
                            {
                                AttributeSourceCS.Replace("public sealed", accessibility + " sealed"),
                                """
                                public static class Library
                                {
                                    [System.Diagnostics.CodeAnalysis.RequireNamedArguments]
                                    public static void M(int count) { }
                                }
                                """,
                            },
                        },
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        """
                        class C
                        {
                            static void Test(int count)
                            {
                                Library.M(count: 1);
                                Library.M(count);
                            }
                        }
                        """,
                    },
                },
            };
            await test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task AttributeInSeveralReferencedAssemblies_Diagnostic_CS()
        {
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        """
                        class C
                        {
                            static void Test()
                            {
                                First.M([|1|]);
                                Second.M([|2|]);
                            }
                        }
                        """,
                    },
                    AdditionalProjectReferences = { "FirstLibrary", "SecondLibrary" },
                    AdditionalProjects =
                    {
                        ["FirstLibrary"] =
                        {
                            Sources =
                            {
                                AttributeSourceCS,
                                """
                                public static class First
                                {
                                    [System.Diagnostics.CodeAnalysis.RequireNamedArguments]
                                    public static void M(int count) { }
                                }
                                """,
                            },
                        },
                        ["SecondLibrary"] =
                        {
                            Sources =
                            {
                                AttributeSourceCS,
                                """
                                public static class Second
                                {
                                    [System.Diagnostics.CodeAnalysis.RequireNamedArguments]
                                    public static void M(int count) { }
                                }
                                """,
                            },
                        },
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        """
                        class C
                        {
                            static void Test()
                            {
                                First.M(count: 1);
                                Second.M(count: 2);
                            }
                        }
                        """,
                    },
                },
            };
            await test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task Method_AllParametersAffected_VB()
        {
            await VerifyVBAsync("""
                Imports System.Diagnostics.CodeAnalysis

                Class C
                    <RequireNamedArguments>
                    Sub Instance(first As Integer, second As Integer)
                    End Sub

                    <RequireNamedArguments>
                    Shared Function GenericMethod(Of T)(first As T) As T
                        Return first
                    End Function

                    Sub NotAttributed(first As Integer)
                    End Sub

                    Sub Test()
                        Instance([|1|], [|2|])
                        Me.Instance([|1|], second:=2)
                        Instance(first:=1, [|2|])
                        Instance(second:=2, first:=1)
                        GenericMethod([|""|])
                        NotAttributed(1)
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        public async Task Constructor_Diagnostic_VB()
        {
            await VerifyVBAsync("""
                Imports System
                Imports System.Diagnostics.CodeAnalysis

                Class B
                    <RequireNamedArguments>
                    Sub New(x As Integer)
                    End Sub

                    Sub New()
                        Me.New([|1|])
                    End Sub
                End Class

                Class D
                    Inherits B

                    Sub New()
                        MyBase.New([|1|])
                    End Sub

                    Sub Test(x As Integer)
                        Dim a = New B([|1|])
                        Dim b As New B([|1|])
                        Dim c = New B(x)
                        Dim d = New B(x:=1)
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        public async Task Indexer_Diagnostic_VB()
        {
            await VerifyVBAsync("""
                Imports System.Diagnostics.CodeAnalysis

                Class C
                    <RequireNamedArguments>
                    Default Property Item(row As Integer, column As Integer) As Integer
                        Get
                            Return 0
                        End Get
                        Set(value As Integer)
                        End Set
                    End Property

                    <RequireNamedArguments>
                    ReadOnly Property Named(row As Integer) As Integer
                        Get
                            Return 0
                        End Get
                    End Property

                    Sub Test(c As C, d As Dictionary, column As Integer)
                        Dim e = d!key
                        Dim a = c([|1|], column)
                        c([|1|], [|2|]) = 3
                        c.Item([|1|], [|2|]) += 3
                        Dim b = c.Named([|1|])
                        Dim f = c(row:=1, column:=2)
                    End Sub
                End Class

                Class Dictionary
                    <RequireNamedArguments>
                    Default ReadOnly Property Item(name As String) As Integer
                        Get
                            Return 0
                        End Get
                    End Property
                End Class
                """);
        }

        [TestMethod]
        public async Task Delegate_Diagnostic_VB()
        {
            await VerifyVBAsync("""
                Imports System.Diagnostics.CodeAnalysis

                <RequireNamedArguments>
                Delegate Sub Attributed(count As Integer)

                Delegate Sub NotAttributed(count As Integer)

                Class C
                    Event E As Attributed
                    Event N(count As Integer)

                    Sub Test(a As Attributed, n As NotAttributed, count As Integer)
                        a([|1|])
                        a.Invoke([|1|])
                        a(count)
                        n(1)
                        RaiseEvent E([|1|])
                        RaiseEvent E(count)
                        RaiseEvent E(count:=1)
                        RaiseEvent N(1)
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        public async Task ParameterLevel_OtherParametersUnaffected_VB()
        {
            await VerifyVBAsync("""
                Imports System.Diagnostics.CodeAnalysis

                Class C
                    Shared Sub Log(message As String, <RequireNamedArguments> category As String, level As Integer)
                    End Sub

                    Shared Sub Test()
                        Log("text", [|"Networking"|], 1)
                        Log("text", category:="Networking", 1)
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        [DataRow("[|42|]")]
        [DataRow("[|count + 1|]")]
        [DataRow("[|(count)|]")]
        [DataRow("[|CInt(count)|]")]
        [DataRow("[|CType(count, Integer)|]")]
        [DataRow("[|value|]")]
        [DataRow("[|GetCount()|]")]
        [DataRow("count")]
        [DataRow("COUNT")]
        [DataRow("[count]")]
        [DataRow("Me.Other.Count")]
        [DataRow("count:=value")]
        public async Task SelfDocumentingExpressions_VB(string argument)
        {
            await VerifyVBAsync($$"""
                Imports System.Diagnostics.CodeAnalysis

                Class Holder
                    Public Count As Integer
                End Class

                Class C
                    Private Other As Holder
                    Private value As Integer

                    Function GetCount() As Integer
                        Return 0
                    End Function

                    <RequireNamedArguments>
                    Sub M(count As Integer)
                    End Sub

                    Sub Test(count As Integer)
                        M({{argument}})
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        public async Task ByRefArgumentNeedingConversion_VB()
        {
            await VerifyVBAsync("""
                Imports System.Diagnostics.CodeAnalysis

                Class C
                    <RequireNamedArguments>
                    Shared Sub M(ByRef count As Long)
                    End Sub

                    Shared Sub Test(value As Integer, count As Integer)
                        M([|value|])
                        M(count)
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        public async Task OmittedOptionalAndParameterless_VB()
        {
            await VerifyVBAsync("""
                Imports System.Diagnostics.CodeAnalysis

                Class C
                    <RequireNamedArguments>
                    Shared Sub OptionalParameters(Optional x As Integer = 1, Optional y As Integer = 2, Optional z As Integer = 3)
                    End Sub

                    <RequireNamedArguments>
                    Shared Sub Parameterless()
                    End Sub

                    Shared Sub Test()
                        Parameterless()
                        Parameterless
                        OptionalParameters()
                        OptionalParameters([|1|])
                        OptionalParameters([|1|], , [|3|])
                        OptionalParameters(, [|2|])
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        public async Task ParamArray_VB()
        {
            await VerifyVBAsync("""
                Imports System.Diagnostics.CodeAnalysis

                Class C
                    <RequireNamedArguments>
                    Shared Sub M(first As Integer, ParamArray rest As Integer())
                    End Sub

                    Shared Sub Test(first As Integer)
                        M(first)
                        M(first, 2)
                        M([|1|], 2, 3)
                        M(first, New Integer() {2, 3})
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        public async Task ExtensionMethod_VB()
        {
            await VerifyVBAsync("""
                Imports System.Diagnostics.CodeAnalysis
                Imports System.Runtime.CompilerServices

                Module Extensions
                    <Extension, RequireNamedArguments>
                    Function AddTo(self As Integer, other As Integer) As Integer
                        Return self + other
                    End Function

                    <Extension>
                    Function SecondOnly(self As Integer, first As Integer, <RequireNamedArguments> second As Integer) As Integer
                        Return self
                    End Function

                    Sub Test(value As Integer)
                        Dim a = value.AddTo([|1|])
                        Dim b = value.AddTo(other:=1)
                        Dim c = value.SecondOnly(1, [|2|])
                        Dim d = Extensions.AddTo([|value|], [|1|])
                    End Sub
                End Module
                """);
        }

        [TestMethod]
        public async Task OverrideAndInterface_OnlyBoundDeclarationCounts_VB()
        {
            await VerifyVBAsync("""
                Imports System.Diagnostics.CodeAnalysis

                Interface I
                    <RequireNamedArguments>
                    Sub M(count As Integer)
                End Interface

                Class Impl
                    Implements I

                    Public Sub M(count As Integer) Implements I.M
                    End Sub
                End Class

                Class Base
                    Public Overridable Sub V(count As Integer)
                    End Sub
                End Class

                Class Derived
                    Inherits Base

                    <RequireNamedArguments>
                    Public Overrides Sub V(count As Integer)
                    End Sub
                End Class

                Class C
                    Sub Test(i As I, impl As Impl, b As Base, d As Derived)
                        i.M([|1|])
                        impl.M(1)
                        b.V(1)
                        d.V([|1|])
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        public async Task NamedSyntaxUnavailable_NoDiagnostic_VB()
        {
            await VerifyVBAsync("""
                Imports System
                Imports System.Collections
                Imports System.Diagnostics.CodeAnalysis
                Imports System.Linq.Expressions

                Class MyAttribute
                    Inherits Attribute

                    <RequireNamedArguments>
                    Sub New(x As Integer)
                    End Sub
                End Class

                Class Collection
                    Implements IEnumerable

                    <RequireNamedArguments>
                    Public Sub Add(first As Integer, second As Integer)
                    End Sub

                    Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
                        Return Nothing
                    End Function
                End Class

                <My(1)>
                Class C
                    <RequireNamedArguments>
                    Shared Function M(count As Integer) As Integer
                        Return count
                    End Function

                    Shared Sub Test(o As Object)
                        Dim f As Func(Of Integer, Integer) = AddressOf M
                        Dim e As Expression(Of Func(Of Integer)) = Function() M(1)
                        Dim c = New Collection From {{1, 2}, {3, 4}}
                        o.M(1)
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        public async Task AttributeTypeMissing_NoDiagnostic_VB()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Class C
                    Shared Sub M(count As Integer)
                    End Sub

                    Shared Sub Test()
                        M(1)
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        public async Task AttributeInReferencedAssembly_Diagnostic_VB()
        {
            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        """
                        Class C
                            Shared Sub Test(count As Integer)
                                Library.M([|1|])
                                Library.M(count)
                            End Sub
                        End Class
                        """,
                    },
                    AdditionalProjectReferences = { "Library" },
                    AdditionalProjects =
                    {
                        ["Library", LanguageNames.CSharp] =
                        {
                            Sources =
                            {
                                AttributeSourceCS.Replace("public sealed", "internal sealed"),
                                """
                                public static class Library
                                {
                                    [System.Diagnostics.CodeAnalysis.RequireNamedArguments]
                                    public static void M(int count) { }
                                }
                                """,
                            },
                        },
                    },
                },
            };
            await test.RunAsync(CancellationToken.None);
        }

        private static VerifyCS.Test CreateCSTest(
            string source,
            string fixedSource = null,
            LanguageVersion languageVersion = LanguageVersion.CSharp7_3,
            ReferenceAssemblies referenceAssemblies = null)
        {
            return new VerifyCS.Test
            {
                TestState = { Sources = { source, AttributeSourceCS } },
                FixedState = { Sources = { fixedSource ?? source, AttributeSourceCS } },
                LanguageVersion = languageVersion,
                ReferenceAssemblies = referenceAssemblies ?? AdditionalMetadataReferences.Default,
            };
        }

        private static Task VerifyCSAsync(
            string source,
            string fixedSource = null,
            LanguageVersion languageVersion = LanguageVersion.CSharp7_3,
            ReferenceAssemblies referenceAssemblies = null)
            => CreateCSTest(source, fixedSource, languageVersion, referenceAssemblies).RunAsync(CancellationToken.None);

        private static Task VerifyVBAsync(string source)
            => new VerifyVB.Test { TestState = { Sources = { source, AttributeSourceVB } } }.RunAsync(CancellationToken.None);
    }
}
