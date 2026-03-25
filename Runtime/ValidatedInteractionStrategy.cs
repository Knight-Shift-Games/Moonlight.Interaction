using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moonlight.Interaction
{
    [Serializable]
    public class ValidatedInteractionStrategy
    {
        [SerializeReference] public List<IInteractionValidation> Validations = new();
        [SerializeReference] public IInteractionStrategy Strategy;
    }
}