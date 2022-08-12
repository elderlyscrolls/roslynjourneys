﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ConvertToRecord;
using Microsoft.CodeAnalysis.Editor.UnitTests.CodeActions;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.ConvertToRecord
{
    using VerifyCS = CSharpCodeRefactoringVerifier<CSharpConvertToRecordRefactoringProvider>;

    [UseExportProvider]
    [Trait(Traits.Feature, Traits.Features.CodeActionsConvertToRecord)]
    public class ConvertToRecordTests
    {
        [Fact]
        public async Task TestNoProperties_NoAction()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        // field, not property
        public int f = 0;
    }
}
";
            await TestNoRefactoringAsync(initialMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestPartialClass_NoAction()
        {
            var initialMarkup = @"
namespace N
{
    public partial class [|C|]
    {
        public int F { get; init; }
    }

    public partial class C
    {
        public bool B { get; init; }
    }
}
";
            await TestNoRefactoringAsync(initialMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestExplicitProperty_NoAction1()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        private int f;

        public int P 
        {
            get => f; 
            init => f = value;
        }
    }
}
";
            await TestNoRefactoringAsync(initialMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestExplicitProperty_NoAction2()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        private int f;

        public int P 
        {
            get
            {
                return f;
            }

            init
            {
                f = value;
            }
        }
    }
}
";
            await TestNoRefactoringAsync(initialMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestExplicitProperty_NoAction3()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; } = 4;
    }
}
";
            await TestNoRefactoringAsync(initialMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestPrivateGetProperty_NoAction()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { private get; init; }
    }
}
";
            await TestNoRefactoringAsync(initialMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestSetProperty()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; set; }
    }
}
";
            var fixedMarkup = @"
namespace N
{
    public record [|C|](int P);
}
";
            await TestRefactoringAsync(initialMarkup, fixedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestInitPropertyOnStruct()
        {
            var initialMarkup = @"
namespace N
{
    public struct [|C|]
    {
        public int P { get; init; }
    }
}
";
            var fixedMarkup = @"
namespace N
{
    public record struct [|C|](int P)
    {
        public int P { get; init; } = P;
    }
}
";
            await TestRefactoringAsync(initialMarkup, fixedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestPrivateSetProperty()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; private set; }
    }
}
";
            var fixedMarkup = @"
namespace N
{
    public record [|C|](int P)
    {
        public int P { get; private set; } = P;
    }
}
";
            await TestRefactoringAsync(initialMarkup, fixedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMoveSimpleProperty()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestReadonlyProperty()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; }
    }
}
";
            var fixedMarkup = @"
namespace N
{
    public record [|C|](int P);
}
";
            await TestRefactoringAsync(initialMarkup, fixedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestInitPropertyOnReadonlyStruct()
        {
            var initialMarkup = @"
namespace N
{
    public readonly struct [|C|]
    {
        public int P { get; init; }
    }
}
";
            var fixedMarkup = @"
namespace N
{
    public readonly record struct [|C|](int P);
}
";
            await TestRefactoringAsync(initialMarkup, fixedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertySimpleInheritance()
        {
            var initialMarkup = @"
namespace N
{
    public class B
    {
    }

    public class [|C|] : B
    {
        public int P { get; init; }
    }
}
";
            // three of the same error on C because the generated
            // EqualityConstract, Equals, and PrintMembers are all declared override
            // and there's nothing to override.
            // The other errors are copy constructor expected in B, and the
            // "records can't inherit from class" on B as well
            var changedMarkup = @"
namespace N
{
    public class B
    {
    }

    public record {|CS0115:{|CS0115:{|CS0115:{|CS8867:C|}|}|}|}(int P) : {|CS8864:B|};
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertySimpleInterfaceInheritance()
        {
            var initialMarkup = @"
namespace N
{
    public interface IInterface
    {
        public int Foo();
    }

    public class [|C|] : IInterface
    {
        public int P { get; init; }

        public int Foo()
        {
            return P;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public interface IInterface
    {
        public int Foo();
    }

    public record C(int P) : IInterface
    {

        public int Foo()
        {
            return P;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMoveMultipleProperties()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMoveMultiplePropertiesOnStruct()
        {
            var initialMarkup = @"
namespace N
{
    public struct [|C|]
    {
        public int P { get; set; }
        public bool B { get; set; }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record struct C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMoveMultiplePropertiesOnReadonlyStruct()
        {
            var initialMarkup = @"
namespace N
{
    public readonly struct [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public readonly record struct C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        // if there are both init and set properties, convert both but keep set property override
        [Fact]
        public async Task TestSetAndInitProperties()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; set; }

        public int Q { get; init; }
    }
}
";
            var fixedMarkup = @"
namespace N
{
    public record [|C|](int P, int Q)
    {
        public int P { get; set; } = P;
    }
}
";
            await TestRefactoringAsync(initialMarkup, fixedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMoveMultiplePropertiesOnGeneric()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C<TA, TB>|]
    {
        public TA? P { get; init; }
        public TB? B { get; init; }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C<TA, TB>(TA? P, TB? B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMoveMultiplePropertiesOnGenericWithConstraints()
        {
            var initialMarkup = @"
using System;
using System.Collections.Generic;

namespace N
{
    public class [|C|]<TA, TB>
        where TA : Exception
        where TB : IEnumerable<TA>
    {
        public TA? P { get; init; }
        public TB? B { get; init; }
    }
}
";
            var changedMarkup = @"
using System;
using System.Collections.Generic;

namespace N
{
    public record C<TA, TB>(TA? P, TB? B) where TA : Exception
            where TB : IEnumerable<TA>;
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesWithAttributes()
        {
            var initialMarkup = @"
using System;

namespace N
{
    public class [|C|]
    {
        [Obsolete(""P is Obsolete"", error: true)]
        public int P { get; init; }

        [Obsolete(""B will be obsolete, error: false"")]
        public bool B { get; init; }
    }
}
";
            var changedMarkup = @"
using System;

namespace N
{
    public record C([property: Obsolete(""P is Obsolete"", error: true)] int P, [property: Obsolete(""B will be obsolete, error: false"")] bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesWithAttributesAndComments1()
        {
            var initialMarkup = @"
using System;

namespace N
{
    public class [|C|]
    {
        // comment before
        [Obsolete(""P is Obsolete"", error: true)]
        public int P { get; init; }

        [Obsolete(""B will be obsolete, error: false"")]
        // comment after
        public bool B { get; init; }
    }
}
";
            var changedMarkup = @"
using System;

namespace N
{
    // comment before
    // comment after
    public record C([property: Obsolete(""P is Obsolete"", error: true)] int P, [property: Obsolete(""B will be obsolete, error: false"")] bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesWithAttributesAndComments2()
        {
            var initialMarkup = @"
using System;

namespace N
{
    public class [|C|]
    {
        [/*comment before*/ Obsolete(""P is Obsolete"", error: true)]
        public int P { get; init; }

        [Obsolete(""B will be obsolete, error: false"") /* comment after*/]
        public bool B { get; init; }
    }
}
";
            var changedMarkup = @"
using System;

namespace N
{
    public record C([/*comment before*/ property: Obsolete(""P is Obsolete"", error: true)] int P, [property: Obsolete(""B will be obsolete, error: false"") /* comment after*/] bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteSimpleEquals1()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            return other is C otherC && otherC.P == P && otherC.B == B;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteSimpleEqualsWithFields()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }
        private int num = 10;

        public override bool Equals(object? other)
        {
            return other is C otherC && otherC.P == P && otherC.B == B && num == otherC.num;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B)
    {
        private int num = 10;
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepSimpleEqualsWithConstFields()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }
        private const int num = 10;

        public override bool Equals(object? other)
        {
            return other is C otherC && otherC.P == P && otherC.B == B && num == C.num;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B)
    {
        private const int num = 10;

        public override bool {|CS0111:Equals|}(object? other)
        {
            return other is C otherC && otherC.P == P && otherC.B == B && num == C.num;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteSimpleEqualsWithConstAndStaticFieldsAndProps()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }
        private const int num = 10;
        public static int Foo = 100;
        public static bool StaticProp { get; set; } = false;

        public override bool Equals(object? other)
        {
            return other is C otherC && otherC.P == P && otherC.B == B;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B)
    {
        private const int num = 10;
        public static int Foo = 100;
        public static bool StaticProp { get; set; } = false;
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteSimpleEquals2()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            return other is C otherC && P == otherC.P && B == otherC.B;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteInvertedEquals()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            return !(other is not C otherC || otherC.P != P || otherC.B != B);
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepEqualsDoubleComparison()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            return other is C otherC && otherC.P == P && otherC.B == B && otherC.P == P;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B)
    {

        public override bool {|CS0111:Equals|}(object? other)
        {
            return other is C otherC && otherC.P == P && otherC.B == B && otherC.P == P;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepEqualsMissingComparison()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            return other is C otherC && otherC.P == P;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B)
    {

        public override bool {|CS0111:Equals|}(object? other)
        {
            return other is C otherC && otherC.P == P;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepEqualsSelfComparison1()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            return other is C otherC && this.P == P && otherC.B == B;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B)
    {

        public override bool {|CS0111:Equals|}(object? other)
        {
            return other is C otherC && this.P == P && otherC.B == B;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepEqualsSelfComparison2()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            return other is C otherC && otherC.P == otherC.P && otherC.B == B;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B)
    {

        public override bool {|CS0111:Equals|}(object? other)
        {
            return other is C otherC && otherC.P == otherC.P && otherC.B == B;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepEqualsWithSideEffect()
        {
            var initialMarkup = @"
using System;

namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            Console.WriteLine(""testing equals..."");
            return other is C otherC && otherC.P == P && otherC.B == B;
        }
    }
}
";
            var changedMarkup = @"
using System;

namespace N
{
    public record C(int P, bool B)
    {

        public override bool {|CS0111:Equals|}(object? other)
        {
            Console.WriteLine(""testing equals..."");
            return other is C otherC && otherC.P == P && otherC.B == B;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepEqualsIncorrectComparison()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public int B { get; init; }

        public override bool Equals(object? other)
        {
            return other is C otherC && otherC.P == B && otherC.B == P;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, int B)
    {

        public override bool {|CS0111:Equals|}(object? other)
        {
            return other is C otherC && otherC.P == B && otherC.B == P;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepWrongInvertedEquals()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            return !(other is C otherC && otherC.P == P && otherC.B == B);
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B)
    {

        public override bool {|CS0111:Equals|}(object? other)
        {
            return !(other is C otherC && otherC.P == P && otherC.B == B);
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepOrEquals()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            if (other is C otherC)
            {
                return otherC.P == P || otherC.B == B;
            }

            return false;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B)
    {

        public override bool {|CS0111:Equals|}(object? other)
        {
            if (other is C otherC)
            {
                return otherC.P == P || otherC.B == B;
            }

            return false;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteIfCastEquals1()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            if (other is C otherC)
            {
                return otherC.P == P && otherC.B == B;
            }

            return false;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteIfCastEquals2()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            if (other is C otherC && otherC.P == P && otherC.B == B)
            {
                return true;
            }

            return false;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteInvertedIfCastEquals()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            if (other is not C otherC)
            {
                return false;
            }

            return otherC.P == P && otherC.B == B;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepWrongInvertedIfCastEquals()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            if (other is C)
            {
                return false;
            }

            var otherC = {|CS8600:(C)other|};
            return {|CS8602:otherC|}.P == P && otherC.B == B;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B)
    {

        public override bool {|CS0111:Equals|}(object? other)
        {
            if (other is C)
            {
                return false;
            }

            var otherC = {|CS8600:(C)other|};
            return {|CS8602:otherC|}.P == P && otherC.B == B;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteIfThenCastEquals()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            if (other is C)
            {
                var otherC = (C)other;
                return otherC.P == P && otherC.B == B;
            }

            return false;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteIfChainEquals()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            if (other is not C)
            {
                return false;
            }

            var otherC = (C)other;
            if (P != otherC.P)
            {
                return false;
            }

            if (otherC.B != B)
            {
                return false;
            }

            return true;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteIfElseChainEquals()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            if (other is not C)
            {
                return false;
            }
            else {
                var otherC = (C)other;
                if (P != otherC.P)
                {
                    return false;
                }
                else if (otherC.B != B)
                {
                    return false;
                }

                return true;
            }
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteInvertedIfChainEquals()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            if (other is C)
            {
                var otherC = (C)other;
                if (otherC.P == P)
                {
                    if (otherC.B == B)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteAsCastEquals()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            var otherC = other as C;
            return otherC != null && otherC.P == P && otherC.B == B;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteAsCastEqualsWithIsNotNull()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            var otherC = other as C;
            return otherC is not null && otherC.P == P && otherC.B == B;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepAsCastEqualsWithIncorrectIsNull()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            var otherC = other as C;
            return otherC is null && {|CS8602:otherC|}.P == P && otherC.B == B;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B)
    {

        public override bool {|CS0111:Equals|}(object? other)
        {
            var otherC = other as C;
            return otherC is null && {|CS8602:otherC|}.P == P && otherC.B == B;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteAsCastEqualsWithIsNull()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override bool Equals(object? other)
        {
            var otherC = other as C;
            return !(otherC is null || otherC.P != P || otherC.B != B);
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteSimpleTypeEquals()
        {
            var initialMarkup = @"
using System;

namespace N
{
    public class [|C|] : IEquatable<C>
    {
        public int P { get; init; }
        public bool B { get; init; }

        public bool Equals(C? otherC)
        {
            return {|CS8602:otherC|}.P == P && otherC.B == B;
        }
    }
}
";
            var changedMarkup = @"
using System;

namespace N
{
    public record C(int P, bool B) : IEquatable<C>;
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteSimpleTypeEqualsAndObjectEquals()
        {
            var initialMarkup = @"
using System;

namespace N
{
    public class [|C|] : IEquatable<C>
    {
        public int P { get; init; }
        public bool B { get; init; }

        public bool Equals(C? otherC)
        {
            return {|CS8602:otherC|}.P == P && otherC.B == B;
        }

        public override bool Equals(object? other)
        {
            return Equals(other as C);
        }
    }
}
";
            var changedMarkup = @"
using System;

namespace N
{
    public record C(int P, bool B) : IEquatable<C>;
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteTypeEqualsIfChain()
        {
            var initialMarkup = @"
using System;

namespace N
{
    public class [|C|] : IEquatable<C>
    {
        public int P { get; init; }
        public bool B { get; init; }

        public bool Equals(C? otherC)
        {
            if (otherC == null)
            {
                return false;
            }

            if (P != otherC.P)
            {
                return false;
            }

            if (otherC.B != B)
            {
                return false;
            }

            return true;
        }
    }
}
";
            var changedMarkup = @"
using System;

namespace N
{
    public record C(int P, bool B) : IEquatable<C>;
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteHashCode1()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override int GetHashCode()
        {
            var hashCode = 339610899;
            hashCode = hashCode * -1521134295 + P.GetHashCode();
            hashCode = hashCode * -1521134295 + B.GetHashCode();
            return hashCode;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteHashCode2()
        {
            var initialMarkup = @"
using System.Collections.Generic;

namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override int GetHashCode()
        {
            var hashCode = 339610899;
            hashCode = hashCode * -1521134295 + P.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<bool>.Default.GetHashCode(B);
            return hashCode;
        }
    }
}
";
            var changedMarkup = @"
using System.Collections.Generic;

namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepComplexHashCode()
        {
            var initialMarkup = @"
using System;
using System.Collections.Generic;

namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public override int GetHashCode()
        {
            var hashCode = 339610899;
            Console.WriteLine(""This could potentially be a side effect"");
            hashCode = hashCode * -1521134295 + P.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<bool>.Default.GetHashCode(B);
            return hashCode;
        }
    }
}
";
            var changedMarkup = @"
using System;
using System.Collections.Generic;

namespace N
{
    public record C(int P, bool B)
    {

        public override int GetHashCode()
        {
            var hashCode = 339610899;
            Console.WriteLine(""This could potentially be a side effect"");
            hashCode = hashCode * -1521134295 + P.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<bool>.Default.GetHashCode(B);
            return hashCode;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteSimpleEqualOperatorsWithNullableObjectParam1()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public static bool operator ==(C c1, object? c2) {
            return c1.Equals(c2);
        }

        public static bool operator !=(C c1, object? c2) {
            return !(c1 == c2);
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteSimpleEqualOperatorsWithNullableObjectParam2()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public static bool operator ==(C c1, object? c2) {
            return c1.Equals(c2);
        }

        public static bool operator !=(C c1, object? c2) {
            return !c1.Equals(c2);
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteSimpleEqualOperatorsWithNullableObjectParam3()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public static bool operator ==(C c1, object? c2) {
            return c2!.Equals(c1);
        }

        public static bool operator !=(C c1, object? c2) {
            return !(c2 == c1);
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteSimpleEqualOperatorsWithObjectParam()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public static bool operator ==(C c1, object c2) {
            return c1.Equals(c2);
        }

        public static bool operator !=(C c1, object c2) {
            return !(c1 == c2);
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteEqualOperatorsWithExpressionBodies()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public static bool operator ==(C c1, object c2)
            => c1.Equals(c2);

        public static bool operator !=(C c1, object c2)
            => !(c1 == c2);
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteSimpleEqualOperatorsWithSameTypeParams()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public static bool operator ==(C c1, C c2) {
            return c1.Equals(c2);
        }

        public static bool operator !=(C c1, C c2) {
            return !(c1 == c2);
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteSimpleEqualOperatorsWithNullableTypeParams()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public static bool operator ==(C? c1, C? c2) {
            return c1!.Equals(c2);
        }

        public static bool operator !=(C? c1, C? c2) {
            return !(c1 == c2);
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepSideEffectOperator1()
        {
            var initialMarkup = @"
using System;

namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public static bool operator ==(C c1, object? c2)
        {
            Console.WriteLine(""checking equality"");
            return c1.Equals(c2);
        }

        public static bool operator !=(C c1, object? c2)
        {
            return !(c1 == c2);
        }
    }
}
";
            var changedMarkup = @"
using System;

namespace N
{
    public record C(int P, bool B)
    {

        public static bool operator ==(C c1, object? c2)
        {
            Console.WriteLine(""checking equality"");
            return c1.Equals(c2);
        }

        public static bool operator !=(C c1, object? c2)
        {
            return !(c1 == c2);
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepSideEffectOperator2()
        {
            var initialMarkup = @"
using System;

namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public static bool operator ==(C c1, object? c2)
        {
            return c1.Equals(c2);
        }

        public static bool operator !=(C c1, object? c2)
        {
            Console.WriteLine(""checking equality"");
            return !(c1 == c2);
        }
    }
}
";
            var changedMarkup = @"
using System;

namespace N
{
    public record C(int P, bool B)
    {

        public static bool operator ==(C c1, object? c2)
        {
            return c1.Equals(c2);
        }

        public static bool operator !=(C c1, object? c2)
        {
            Console.WriteLine(""checking equality"");
            return !(c1 == c2);
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepSideEffectOperator_WhenSameParamUsed1()
        {
            var initialMarkup = @"
using System;

namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public static bool operator ==(C c1, object? c2)
        {
            return c1.Equals(c1);
        }

        public static bool operator !=(C c1, object? c2)
        {
            return !(c1 == c2);
        }
    }
}
";
            var changedMarkup = @"
using System;

namespace N
{
    public record C(int P, bool B)
    {

        public static bool operator ==(C c1, object? c2)
        {
            return c1.Equals(c1);
        }

        public static bool operator !=(C c1, object? c2)
        {
            return !(c1 == c2);
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepSideEffectOperator_WhenSameParamUsed2()
        {
            var initialMarkup = @"
using System;

namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public static bool operator ==(C c1, object? c2)
        {
            return c1.Equals(c2);
        }

        public static bool operator !=(C c1, object? c2)
        {
            return !(c1 == c1);
        }
    }
}
";
            var changedMarkup = @"
using System;

namespace N
{
    public record C(int P, bool B)
    {

        public static bool operator ==(C c1, object? c2)
        {
            return c1.Equals(c2);
        }

        public static bool operator !=(C c1, object? c2)
        {
            return !(c1 == c1);
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteClone()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public C Clone()
        {
            return this;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteSimpleCopyConstructor()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public C(C other)
        {
            P = other.P;
            B = other.B;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteCopyConstructorWithFields()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }
        int foo = 0;

        public C(C other)
        {
            P = other.P;
            B = other.B;
            foo = other.foo;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B)
    {
        int foo = 0;
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteCopyConstructorWithConstAndStaticFieldsAndProps()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }
        const int foo = 0;
        public static int statFoo = 10;
        public static bool StaticProp { get; set; } = false;

        public C(C other)
        {
            P = other.P;
            B = other.B;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B)
    {
        const int foo = 0;
        public static int statFoo = 10;
        public static bool StaticProp { get; set; } = false;
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepCopyConstructorWithoutFieldAccess()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }
        private int foo = 0;

        public C(C other)
        {
            P = other.P;
            B = other.B;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B)
    {
        private int foo = 0;

        public C(C other)
        {
            P = other.P;
            B = other.B;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteSimplePrimaryConstructor()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public C(int p, bool b)
        {
            P = p;
            B = b;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndDeleteSimpleExpressionPrimaryConstructor()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }

        public C(int p)
            => P = p;
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndModifyOrderFromPrimaryConstructor()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public C(bool b, int p)
        {
            P = p;
            B = b;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(bool B, int P);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndModifyPrimaryConstructorOrderAndDefaults()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public C(bool b = false, int p = 0)
        {
            P = p;
            B = b;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(bool B = false, int P = 0);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndProvideThisInitializerValuesWithOperators()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public C(bool b, int p)
        {
            P = p;
            B = b;
        }

        public C(bool b1, bool b2, bool b3)
        {
            P = b1 ? 1 : 0;
            B = !b2 == b3;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(bool B, int P)
    {

        public C(bool b1, bool b2, bool b3) : this(!b2 == b3, b1 ? 1 : 0)
        {
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndProvideThisInitializerValuesWithReferences()
        {
            var initialMarkup = @"
namespace N
{
    public record R(int Foo, int Bar)
    {
        public int field = 10;

        public bool IsBarNum(int num)
        {
            return Bar == num;
        }
    }

    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public C(bool b, int p)
        {
            P = p;
            B = b;
        }

        public C(R r)
        {
            P = r.field;
            B = r.IsBarNum(r.Foo);
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record R(int Foo, int Bar)
    {
        public int field = 10;

        public bool IsBarNum(int num)
        {
            return Bar == num;
        }
    }

    public record C(bool B, int P)
    {

        public C(R r) : this(r.IsBarNum(r.Foo), r.field)
        {
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndProvideThisInitializerValuesWithNullOperations()
        {
            var initialMarkup = @"
namespace N
{
    public record R(int? Foo, int Bar)
    {
        public int field = 10;

        public bool IsBarNum(int num)
        {
            return Bar == num;
        }
    }

    public class [|C|]
    {
        public int P { get; init; }
        public bool? B { get; init; }

        public C(bool? b, int p)
        {
            P = p;
            B = b;
        }

        public C(R? r, int backup)
        {
            P = r?.Foo ?? 10;
            B = r?.IsBarNum(backup);
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record R(int? Foo, int Bar)
    {
        public int field = 10;

        public bool IsBarNum(int num)
        {
            return Bar == num;
        }
    }

    public record C(bool? B, int P)
    {

        public C(R? r, int backup) : this(r?.IsBarNum(backup), r?.Foo ?? 10)
        {
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndProvideThisInitializerValuesWithIsExpressions()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public C(bool b, int p)
        {
            P = p;
            B = b;
        }

        public C(object b1, bool b2, object b3)
        {
            P = b1 is int ? 1 : 0;
            B = !b2 && b3 is C { P: 10 };
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(bool B, int P)
    {

        public C(object b1, bool b2, object b3) : this(!b2 && b3 is C { P: 10 }, b1 is int ? 1 : 0)
        {
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndProvideThisInitializerValuesWithSwitchExpressions()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public C(bool b, int p)
        {
            P = p;
            B = b;
        }

        public C(int f1, bool b2, bool b3)
        {
            P = f1 switch
            {
                1 => 0,
                0 => 1,
                _ => default
            };
            B = !b2 && b3;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(bool B, int P)
    {

        public C(int f1, bool b2, bool b3) : this(!b2 && b3, f1 switch
        {
            1 => 0,
            0 => 1,
            _ => default
        })
        {
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndProvideThisInitializerValuesWithSideEffects()
        {
            var initialMarkup = @"
using System;

namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public C(bool b, int p)
        {
            P = p;
            B = b;
        }

        public C(bool b1, bool b2)
        {
            P = b1 ? 1 : 0;
            Console.WriteLine(""Side effect"");
            B = !b2;
        }
    }
}
";
            var changedMarkup = @"
using System;

namespace N
{
    public record C(bool B, int P)
    {

        public C(bool b1, bool b2) : this(!b2, b1 ? 1 : 0)
        {
            Console.WriteLine(""Side effect"");
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndProvideThisInitializerValuesComplex()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public C(bool b, int p)
        {
            P = p;
            B = b;
        }

        public C(bool b1, bool b2)
        {
            P = b1 ? 1 : 0;
            var b = !b2;
            B = b;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(bool B, int P)
    {

        public C(bool b1, bool b2) : this(default, b1 ? 1 : 0)
        {
            var b = !b2;
            B = b;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndProvideThisInitializerDefaultAndNull()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {
        public int? P { get; init; }
        public bool B { get; init; }

        public C(bool b, int? p)
        {
            P = p;
            B = b;
        }

        public C(bool b1, bool b2)
        {
            var b = !b2 || b2;
            B = b;
        }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(bool B, int? P)
    {

        public C(bool b1, bool b2) : this(default, null)
        {
            var b = !b2 || b2;
            B = b;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepComplexPrimaryConstructor1()
        {
            var initialMarkup = @"
using System;

namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public C(int p, bool b)
        {
            Console.WriteLine(""Constructing C..."");
            P = p;
            B = b;
        }
    }
}
";
            var changedMarkup = @"
using System;

namespace N
{
    public record C(int P, bool B)
    {

        public {|CS0111:{|CS8862:C|}|}(int p, bool b)
        {
            Console.WriteLine(""Constructing C..."");
            P = p;
            B = b;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepComplexPrimaryConstructor2()
        {
            var initialMarkup = @"
using System;

namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public C(int p, bool b)
        {
            P = p + 1;
            B = b;
        }
    }
}
";
            var changedMarkup = @"
using System;

namespace N
{
    public record C(int P, bool B)
    {

        public {|CS0111:{|CS8862:C|}|}(int p, bool b)
        {
            P = p + 1;
            B = b;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesAndKeepComplexPrimaryConstructor3()
        {
            var initialMarkup = @"
using System;

namespace N
{
    public class [|C|]
    {
        public int P { get; init; }
        public bool B { get; init; }

        public C(int p, bool b)
        {
            B = b;
        }
    }
}
";
            var changedMarkup = @"
using System;

namespace N
{
    public record C(int P, bool B)
    {

        public {|CS0111:{|CS8862:C|}|}(int p, bool b)
        {
            B = b;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesWithSimpleDocComments()
        {
            var initialMarkup = @"
namespace N
{

    /// <summary>
    /// some summary
    /// </summary>
    public class [|C|]
    {

        /// <summary>
        /// P is an int
        /// </summary>
        public int P { get; init; }

        /// <summary>
        /// B is a bool
        /// </summary>
        public bool B { get; init; }
    }
}
";
            var changedMarkup = @"
namespace N
{
    /// <summary>
    /// some summary
    /// </summary>
    /// <param name=""P""> P is an int </param>
    /// <param name=""B""> B is a bool </param>
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesWithMultilineDocComments()
        {
            var initialMarkup = @"
namespace N
{

    /** 
     * <summary>
     * some summary
     * </summary>
     */
    public class [|C|]
    {

        /** 
         * <summary>
         * P is an int
         * </summary>
         */
        public int P { get; init; }

        /** 
         * <summary>
         * B is a bool
         * </summary>
         */
        public bool B { get; init; }
    }
}
";
            // this is what it should be
            //            var changedMarkup = @"
            //namespace N
            //{
            //    /** 
            //     * <summary>
            //     * some summary
            //     * </summary>
            //     * <param name=""P""> P is an int </param>
            //     * <param name=""B""> B is a bool </param>
            //     */
            //    public record C(int P, bool B);
            //}
            //";

            // this is what it is currently
            var changedMarkup = @"
namespace N
{
    /** 
         * <summary>
         * some summary
         * </summary>
         * <param name=""P""> P is an int </param>
         * <param name=""B""> B is a bool </param>
         */
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesWithMultilineDocComments_NoClassSummary()
        {
            var initialMarkup = @"
namespace N
{

    public class [|C|]
    {

        /** 
         * <summary>
         * P is an int
         * </summary>
         */
        public int P { get; init; }

        /** 
         * <summary>
         * B is a bool
         * </summary>
         */
        public bool B { get; init; }
    }
}
";
            // this is what it should be
            //            var changedMarkup = @"
            //namespace N
            //{
            //    /**
            //     * <param name=""P""> P is an int </param>
            //     * <param name=""B""> B is a bool </param>
            //     */
            //    public record C(int P, bool B);
            //}
            //";
            // this is what it is currently
            var changedMarkup = @"
namespace N
{
    /**
             *<param name=""P""> P is an int </param>
             * <param name=""B""> B is a bool </param>
    */
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesWithMixedDocComments1()
        {
            // class-level comment should be default
            var initialMarkup = @"
namespace N
{

    /// <summary>
    /// some summary
    /// </summary>
    public class [|C|]
    {

        /** 
         * <summary>
         * P is an int
         * </summary>
         */
        public int P { get; init; }

        /** 
         * <summary>
         * B is a bool
         * </summary>
         */
        public bool B { get; init; }
    }
}
";
            var changedMarkup = @"
namespace N
{
    /// <summary>
    /// some summary
    /// </summary>
    /// <param name=""P""> P is an int </param>
    /// <param name=""B""> B is a bool </param>
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesWithMixedDocComments2()
        {
            // class-level comment should be default
            var initialMarkup = @"
namespace N
{

    /** 
     * <summary>
     * some summary
     * </summary>
     */
    public class [|C|]
    {

        /// <summary>
        /// P is an int
        /// </summary>
        public int P { get; init; }

        /// <summary>
        /// B is a bool
        /// </summary>
        public bool B { get; init; }
    }
}
";
            // This is what it should be
            //            var changedMarkup = @"
            //namespace N
            //{
            //    /** 
            //     * <summary>
            //     * some summary
            //     * </summary>
            //     * <param name=""P""> P is an int </param>
            //     * <param name=""B""> B is a bool </param>
            //     */
            //    public record C(int P, bool B);
            //}
            //";

            // this is what it is right now
            var changedMarkup = @"
namespace N
{
    /** 
         * <summary>
         * some summary
         * </summary>
         * <param name=""P""> P is an int </param>
         * <param name=""B""> B is a bool </param>
         */
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesWithMixedDocComments3()
        {
            var initialMarkup = @"
namespace N
{

    /** 
     * <summary>
     * some summary
     * </summary>
     */
    public class [|C|]
    {

        /// <summary>
        /// P is an int
        /// with a multiline comment
        /// </summary>
        public int P { get; init; }

        /// <summary>
        /// B is a bool
        /// </summary>
        public bool B { get; init; }
    }
}
";
            // this is what it should be
            //            var changedMarkup = @"
            //namespace N
            //{
            //    /** 
            //     * <summary>
            //     * some summary
            //     * </summary>
            //     * <param name=""P""> P is an int
            //     * with a multiline comment </param>
            //     * <param name=""B""> B is a bool </param>
            //     */
            //    public record C(int P, bool B);
            //}
            //";

            // this is what it actually is
            var changedMarkup = @"
namespace N
{
    /** 
         * <summary>
         * some summary
         * </summary>
         * <param name=""P""> P is an int
         * with a multiline comment </param>
         * <param name=""B""> B is a bool </param>
         */
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesWithDocComments_NoClassSummary()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C|]
    {

        /// <summary>
        /// P is an int
        /// </summary>
        public int P { get; init; }

        /// <summary>
        /// B is a bool
        /// </summary>
        public bool B { get; init; }
    }
}
";
            var changedMarkup = @"
namespace N
{
    /// <param name=""P""> P is an int </param>
    /// <param name=""B""> B is a bool </param>
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesWithDocComments_MissingPropertySummary()
        {
            var initialMarkup = @"
namespace N
{

    /// <summary>
    /// some summary
    /// </summary>
    public class [|C|]
    {

        public int P { get; init; }

        /// <summary>
        /// B is a bool
        /// </summary>
        public bool B { get; init; }
    }
}
";
            var changedMarkup = @"
namespace N
{
    /// <summary>
    /// some summary
    /// </summary>
    /// <param name=""P""></param>
    /// <param name=""B""> B is a bool </param>
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesWithDocComments_AdditionalClassSection()
        {
            var initialMarkup = @"
namespace N
{

    /// <summary>
    /// some summary
    /// </summary>
    /// <remarks>
    /// Some remarks as well
    /// </reamrks>
    public class [|C|]
    {

        /// <summary>
        /// P is an int
        /// </summary>
        public int P { get; init; }

        /// <summary>
        /// B is a bool
        /// </summary>
        public bool B { get; init; }
    }
}
";
            var changedMarkup = @"
namespace N
{
    /// <summary>
    /// some summary
    /// </summary>
    /// <param name=""P""> P is an int </param>
    /// <param name=""B""> B is a bool </param>
    /// <remarks>
    /// Some remarks as well
    /// </reamrks>
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesWithDocComments_NestedPropertyElements()
        {
            var initialMarkup = @"
namespace N
{

    /// <summary>
    /// some summary
    /// </summary>
    public class [|C|]
    {

        /// <summary>
        /// P is an int <see cref=""C.B""/>
        /// </summary>
        public int P { get; init; }

        /// <summary>
        /// B is a bool
        /// <c> Some code text </c>
        /// </summary>
        public bool B { get; init; }
    }
}
";
            var changedMarkup = @"
namespace N
{
    /// <summary>
    /// some summary
    /// </summary>
    /// <param name=""P""> P is an int <see cref=""C.B""/> </param>
    /// <param name=""B""> B is a bool
    /// <c> Some code text </c> </param>
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesWithDocAndNonDocComments1()
        {
            // we should try to keep the order in the same as the order on the class comments
            var initialMarkup = @"
namespace N
{

    // Non-Doc comment before class
    /// <summary>
    /// some summary
    /// </summary>
    public class [|C|]
    {

        // Non-Doc property comment for P
        /// <summary>
        /// P is an int
        /// </summary>
        public int P { get; init; }

        // Non-Doc property comment for B
        /// <summary>
        /// B is a bool
        /// </summary>
        public bool B { get; init; }
    }
}
";
            var changedMarkup = @"
namespace N
{
    // Non-Doc comment before class
    // Non-Doc property comment for P
    // Non-Doc property comment for B
    /// <summary>
    /// some summary
    /// </summary>
    /// <param name=""P""> P is an int </param>
    /// <param name=""B""> B is a bool </param>
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestMovePropertiesWithDocAndNonDocComments2()
        {
            // we should try to keep the order in the same as the order on the class comments
            var initialMarkup = @"
namespace N
{

    /// <summary>
    /// some summary
    /// </summary>
    // Non-Doc comment after class
    public class [|C|]
    {

        // Non-Doc property comment for P
        /// <summary>
        /// P is an int
        /// </summary>
        public int P { get; init; }

        // Non-Doc property comment for B
        /// <summary>
        /// B is a bool
        /// </summary>
        public bool B { get; init; }
    }
}
";
            var changedMarkup = @"
namespace N
{
    /// <summary>
    /// some summary
    /// </summary>
    /// <param name=""P""> P is an int </param>
    /// <param name=""B""> B is a bool </param>
    // Non-Doc comment after class
    // Non-Doc property comment for P
    // Non-Doc property comment for B
    public record C(int P, bool B);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        #region selection

        [Fact]
        public async Task TestSelectOnProperty_NoAction()
        {
            var initialMarkup = @"
namespace N
{
    public class C
    {
        public int [|P|] { get; init; }
    }
}
";
            await TestNoRefactoringAsync(initialMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestSelectOnNamespace_NoAction()
        {
            var initialMarkup = @"
namespace [|N|]
{
    public class C
    {
        public int P { get; init; }
    }
}
";
            await TestNoRefactoringAsync(initialMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestSelectLargeRegionIncludingNamespace_NoAction()
        {
            var initialMarkup = @"
namespace [|N
{
    public clas|]s C
    {
        public int P { get; init; }
    }
}
";
            await TestNoRefactoringAsync(initialMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestSelectMultipleMembersWithinClass()
        {
            var initialMarkup = @"
namespace N
{
    public class C
    {
        [|public int P { get; init; }

        public int Foo()
        {
            return 0;
        }|]
    }
}
";
            var fixedMarkup = @"
namespace N
{
    public record C(int P)
    {

        public int Foo()
        {
            return 0;
        }
    }
}
";
            await TestRefactoringAsync(initialMarkup, fixedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestSelectRegionIncludingClass()
        {
            var initialMarkup = @"
namespace N
{
    public class [|C
    {
        public int P { get; init; }|]
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestSelectClassKeyword()
        {
            var initialMarkup = @"
namespace N
{
    public cl[||]ass C
    {
        public int P { get; init; }
    }
}
";
            var changedMarkup = @"
namespace N
{
    public record C(int P);
}
";
            await TestRefactoringAsync(initialMarkup, changedMarkup).ConfigureAwait(false);
        }

        #endregion

        private class Test : VerifyCS.Test
        {
            public Test() { }

            protected override Workspace CreateWorkspaceImpl()
            {
                var workspace = new AdhocWorkspace();

                return workspace;
            }
        }

        private static async Task TestRefactoringAsync(
            string initialMarkup,
            string changedMarkup)
        {
            var test = new Test()
            {
                TestCode = initialMarkup,
                FixedCode = changedMarkup,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp10,
            };
            test.SolutionTransforms.Add((solution, projectId) =>
            {
                var project = solution.GetProject(projectId)!;

                var compilationOptions = (CSharpCompilationOptions)project.CompilationOptions!;
                // enable nullable
                compilationOptions = compilationOptions.WithNullableContextOptions(NullableContextOptions.Enable);
                solution = solution
                    .WithProjectCompilationOptions(projectId, compilationOptions)
                    .WithProjectMetadataReferences(projectId, TargetFrameworkUtil.GetReferences(TargetFramework.Net60));

                return solution;
            });
            await test.RunAsync().ConfigureAwait(false);
        }

        private static Task TestNoRefactoringAsync(
            string initialMarkup) => TestRefactoringAsync(initialMarkup, initialMarkup);
    }
}
