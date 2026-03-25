using System;
using Moonlight.Core;
using Moonlight.UI;
using UnityEngine;
using Zenject;

namespace Moonlight.Interaction
{
    [Serializable]
    public sealed class ToggleAddonInteractionStrategy : IInteractionStrategy
    {
        [field: SerializeField] private StringAsset Addon { get; set; }
        
        [Inject] private IUIService UIService { get; set; }

        public IResult Handle(InteractionContext ctx, IInteractionPayload payload)
        {
            UIService.ToggleAddon(Addon);
            return new ResultSuccess();
        }
    }
}