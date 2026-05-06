using System.Collections.Generic;
using UnityEngine;

namespace Moonlight.Interaction
{
    [CreateAssetMenu(
        fileName = "ValidatedInteractionStrategyGroup",
        menuName = "Moonlight/Interaction/Validated Strategy Group")]
    public sealed class ValidatedInteractionStrategyGroup : ScriptableObject
    {
        [SerializeReference]
        private List<ValidatedInteractionStrategy> _strategies = new();

        public IReadOnlyList<ValidatedInteractionStrategy> Strategies => _strategies;
    }
}
