using System.Collections.Generic;
using UnityEngine;

namespace Moonlight.Interaction
{
    public interface IInteractionOptionFactory
    {
        List<InteractionOption> Generate(GameObject interactable);
    }
}