using UnityEngine;

namespace Moonlight.Interaction
{
    public sealed class InteractionContext
    {
        // Inject anything your options might need:

        public InteractionContext(GameObject player, GameObject target)
        {
            Player = player;
            Target = target;
        }

        public GameObject Player { get; }
        public GameObject Target { get; }
    }
}