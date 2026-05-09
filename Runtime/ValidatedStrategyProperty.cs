using System;
using System.Collections.Generic;
using System.Linq;
using Moonlight.Inventory;
using UnityEngine;

namespace Moonlight.Interaction
{
    [Serializable]
    public sealed class ValidatedStrategyProperty : EntryProperty
    {
        [SerializeReference, AlwaysFromBlueprint]
        private List<ValidatedInteractionStrategy> Strategies = new();

        /// <summary>
        /// Optional reusable bundle of strategies (evaluated after <see cref="Strategies"/>).
        /// </summary>
        [SerializeField, AlwaysFromBlueprint]
        private ValidatedInteractionStrategyGroup StrategyGroup;

        public bool GetValidStrategy(
            InteractionStrategyResolver resolver,
            InteractionContext ctx,
            IInteractionPayload payload,
            out IInteractionStrategy strategy)
        {
            IEnumerable<ValidatedInteractionStrategy> ordered = Strategies;
            if (StrategyGroup != null && StrategyGroup.Strategies.Count > 0)
            {
                ordered = ordered.Concat(StrategyGroup.Strategies);
            }

            return resolver.TryResolve(ordered, ctx, payload, out strategy);
        }

        public override object Clone()
        {
            return new ValidatedStrategyProperty
            {
                Strategies = this.Strategies,
                StrategyGroup = this.StrategyGroup
            };
        }
    }
}
