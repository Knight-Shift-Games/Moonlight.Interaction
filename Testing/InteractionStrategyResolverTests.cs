using System.Collections.Generic;
using Moonlight.Core;
using Moonlight.Interaction;
using NUnit.Framework;
using Zenject;

namespace Moonlight.Interaction.Tests
{
    [TestFixture]
    public class InteractionStrategyResolverTests : ZenjectUnitTestFixture
    {
        [SetUp]
        public void SetUp()
        {
            Container.Bind<InteractionStrategyResolver>().AsSingle();
        }

        [Test]
        public void TryResolve_ReturnsFirstStrategyWithPassingValidations()
        {
            var resolver = Container.Resolve<InteractionStrategyResolver>();
            var ctx = new InteractionContext(null, null);
            var winning = new StubStrategy("win");
            var losing = new StubStrategy("lose");

            var candidates = new[]
            {
                new ValidatedInteractionStrategy
                {
                    Validations = new List<IInteractionValidation> { new FailValidation() },
                    Strategy = losing
                },
                new ValidatedInteractionStrategy
                {
                    Validations = new List<IInteractionValidation> { new PassValidation() },
                    Strategy = winning
                }
            };

            Assert.IsTrue(resolver.TryResolve(candidates, ctx, null, out var strategy));
            Assert.AreSame(winning, strategy);
        }

        [Test]
        public void TryResolve_AllValidationsFail_ReturnsFalse()
        {
            var resolver = Container.Resolve<InteractionStrategyResolver>();
            var ctx = new InteractionContext(null, null);
            var candidates = new[]
            {
                new ValidatedInteractionStrategy
                {
                    Validations = new List<IInteractionValidation> { new FailValidation() },
                    Strategy = new StubStrategy("unused")
                }
            };

            Assert.IsFalse(resolver.TryResolve(candidates, ctx, null, out var strategy));
            Assert.IsNull(strategy);
        }

        sealed class PassValidation : IInteractionValidation
        {
            public IResult Validate(InteractionContext ctx, IInteractionPayload payload) => new ResultSuccess();
        }

        sealed class FailValidation : IInteractionValidation
        {
            public IResult Validate(InteractionContext ctx, IInteractionPayload payload) => new ResultError();
        }

        sealed class StubStrategy : IInteractionStrategy
        {
            readonly string _id;
            public StubStrategy(string id) => _id = id;
            public IResult Handle(InteractionContext ctx, IInteractionPayload payload) => new ResultSuccess();
        }
    }
}
