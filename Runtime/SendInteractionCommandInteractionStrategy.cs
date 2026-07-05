using System;
using Moonlight.Core;
#if QUANTUM_UNITY
using Quantum;
#endif
using Zenject;

namespace Moonlight.Interaction
{
#if QUANTUM_UNITY
    [Serializable]
    public sealed class SendInteractionCommandInteractionStrategy : IInteractionStrategy
    {
        [Inject] private SignalBus SignalBus { get; set; }
        [Inject] private QuantumEntityViewUpdater ViewUpdater { get; set; }
        
        public IResult Handle(InteractionContext ctx, IInteractionPayload payload)
        {
            var interactableView = ctx.Target.GetComponentInParent<QuantumEntityView>();
            
            if (interactableView.EntityRef != EntityRef.None)
            {
                var command = new InteractionCommand();
                command.TargetEntity = interactableView.EntityRef;
                QuantumRunner.Default.Game.SendCommand(command);
            }

            return new ResultSuccess();
        }
    }
#endif
}