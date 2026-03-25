using System;
using System.Collections.Generic;
using System.Linq;
using Moonlight.Inventory;
using UnityEngine;
using Zenject;

namespace Moonlight.Interaction
{
    [Serializable]
    public sealed class ValidatedStrategyProperty : EntryProperty
    {
        [SerializeReference, AlwaysFromBlueprint]
        private List<ValidatedInteractionStrategy> Strategies = new();

        public bool GetValidStrategy(
            DiContainer container,
            InteractionContext ctx,
            IInteractionPayload payload, 
            out IInteractionStrategy strategy)
        {
            var validatedStrategy = Strategies.FirstOrDefault(x => x.Validations.TrueForAll(validation =>
            {
                container.Inject(validation);
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

        public override object Clone()
        {
            return new ValidatedStrategyProperty
            {
                Strategies = this.Strategies
            };
        }
    }
}