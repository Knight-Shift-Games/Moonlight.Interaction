using System.Collections.Generic;
using System.Linq;
using Zenject;

namespace Moonlight.Interaction
{
    public sealed class InteractionStrategyResolver
    {
        private DiContainer Container { get; set; }

        public InteractionStrategyResolver(DiContainer container)
        {
            Container = container;
        }

        /// <summary>
        /// Resolves using an explicit candidate sequence (e.g. blueprint property lists or a strategy group).
        /// </summary>
        public bool TryResolve(IEnumerable<ValidatedInteractionStrategy> candidates,InteractionContext ctx,
            IInteractionPayload payload,out IInteractionStrategy strategy)
        {
            var validatedStrategy = candidates.FirstOrDefault(x => x.Validations.TrueForAll(validation =>
            {
                Container.Inject(validation);
                return validation.Validate(ctx, payload).Success();
            }));

            if (validatedStrategy != null)
            {
                strategy = validatedStrategy.Strategy;
                return true;
            }

            strategy = null;
            return false;
        }
    }
}