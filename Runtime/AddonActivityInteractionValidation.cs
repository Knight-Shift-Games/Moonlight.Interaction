using System;
using Moonlight.Core;
using Moonlight.UI;
using UnityEngine;
using Zenject;

namespace Moonlight.Interaction
{
    [Serializable]
    public sealed class AddonActivityInteractionValidation : IInteractionValidation
    {
        [field: SerializeField] private StringAsset UIAddon { get; set; }
        [field: SerializeField] private bool State { get; set; }
        
        [Inject] private IUIService UIService { get; set; }
        
        public IResult Validate(InteractionContext ctx, IInteractionPayload payload)
        {
            if (UIService.IsAddonActive(UIAddon))
            {
                return new ResultSuccess();
            }
            
            return new ResultError();
        }
    }
}