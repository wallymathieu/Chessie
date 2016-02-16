using System;
using System.Collections.Generic;
using System.Linq;
using Chessie.ErrorHandling;
using Chessie.ErrorHandling.CSharp;
using Microsoft.FSharp.Core;
using NUnit.Framework;

namespace Chessie.CSharp.Test
{
    [TestFixture]
    public class ExtensionsTests
    {
        [Test]
        public void JoinToResultsOfSuccessWorks()
        {
            var result1 = RopResult<int, string>.Succeed(1, "added one");
            var result2 = RopResult<int, string>.Succeed(2, "added two");

            var result3 = result1.Join(result2, _ => 0, _ => -0, (i1, i2) => i1 + i2);

            result3.Match(
                ifSuccess: (x, msgs) =>
                {
                    Assert.AreEqual(3, x);
                    Assert.That(msgs, Is.EquivalentTo(new[] { "added one", "added two" }));
                },
                ifFailure: errs => Assert.Fail());
        }

        [Test]
        public void Test()
        {
            Func<RopResult<string, string>, RopResult<string, string>, RopResult<string, string>, RopResult<string, string>> f = (r1, r2, r3) =>
                from a in r1
                from b in r2
                from c in r3
                select a + b + c;

            f(RopResult<string, string>.Succeed("1"), RopResult<string, string>.Succeed("2"), RopResult<string, string>.Succeed("3")).Match(
                ifSuccess: (s, _) => Assert.That(s, Is.EqualTo("123")),
                ifFailure: _ => Assert.Fail("should not fail"));

            f(RopResult<string, string>.Succeed("1", "msg1"), RopResult<string, string>.Succeed("2", "msg2"), RopResult<string, string>.Succeed("3", "msg3")).Match(
                ifSuccess: (s, list) =>
                {
                    Assert.That(s, Is.EqualTo("123"));
                    Assert.That(list, Is.EquivalentTo(new[] {"msg1", "msg2", "msg3"}));
                },
                ifFailure: list => Assert.Fail("should not fail"));

            f(RopResult<string, string>.FailWith("fail"), RopResult<string, string>.Succeed("2"), RopResult<string, string>.Succeed("3")).Match(
                ifSuccess: (s, _) => Assert.Fail("should fail"),
                ifFailure: list => Assert.That(list, Is.EquivalentTo(new[] { "fail" })));

            f(RopResult<string, string>.Succeed("1"), RopResult<string, string>.FailWith("fail"), RopResult<string, string>.Succeed("3")).Match(
                ifSuccess: (s, _) => Assert.Fail("should fail"),
                ifFailure: list => Assert.That(list, Is.EquivalentTo(new[] { "fail" })));

            f(RopResult<string, string>.Succeed("1"), RopResult<string, string>.FailWith("fail1"), RopResult<string, string>.FailWith("fail2")).Match(
                ifSuccess: (s, _) => Assert.Fail("should fail"),
                ifFailure: list => Assert.That(list, Is.EquivalentTo(new[] { "fail1" })));
        }
        
        [Test]
        public void ToResultOnSomeShouldSucceed()
        {
            var opt = FSharpOption<int>.Some(42);
            var result = opt.ToResult("error");
            result.Match(
                ifSuccess: (x, msgs) =>
                {
                    Assert.AreEqual(42, x);
                    Assert.That(msgs, Is.Empty);
                },
                ifFailure: errs => Assert.Fail());
        }

        [Test]
        public void ToResultOnNoneShoulFail()
        {
            var opt = FSharpOption<int>.None;
            var result = opt.ToResult("error");
            result.Match(
                ifSuccess: (x, _) => Assert.Fail(),
                ifFailure: errs => Assert.That(errs, Is.EquivalentTo(new[] {"error"})));
        }

        [Test]
        public void MapFailureOnSuccessShouldReturnSuccess()
        {
            RopResult<int, string>.Succeed(42, "warn1")
                .MapFailure(list => new[] { 42 })
                .Match(
                    ifSuccess: (v, msgs) =>
                    {
                        Assert.AreEqual(42, v);
                        Assert.That(msgs, Is.Empty);
                    },
                    ifFailure: errs => Assert.Fail());
        }

        [Test]
        public void MapFailureOnFailureShouldMapOverError()
        {
            RopResult<int, string>.FailWith(new[] { "err1", "err2" })
                .MapFailure(_ => new[] { 42 })
                .Match(
                    ifSuccess: (v, msgs) => Assert.Fail(),
                    ifFailure: errs => Assert.That(errs, Is.EquivalentTo(new[] { 42 })));
        }

        [Test]
        public void MapFailureOnFailureShouldMapOverListOfErrors()
        {
            RopResult<int, string>.FailWith(new[] { "err1", "err2" })
                .MapFailure(errs => errs.Select(err =>
                {
                    switch (err)
                    {
                        case "err1": return 42;
                        case "err2": return 43;
                        default: return 0;
                    }
                }))
                .Match(
                    ifSuccess: (v, msgs) => Assert.Fail(),
                    ifFailure: errs => Assert.That(errs, Is.EquivalentTo(new[] { 42, 43 })));
        }
    }
}
