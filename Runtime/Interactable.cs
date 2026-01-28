using System.Collections.Generic;
using System.Linq;
using Moonlight.Core;
using Moonlight.UI;
using UnityEngine;
using Zenject;

namespace Moonlight.Interaction
{
    public sealed class Interactable : MonoBehaviour, IInteractable
    {
        [field: SerializeReference] public List<IInteractionOptionFactory> OptionFactories { get; set; } = new();
        [field: SerializeReference] public List<InteractionOption> DefaultOptions { get; set; } = new();

        [Inject] private DiContainer Container { get; set; }
        [Inject] private InteractableController Controller { get; set; }
        [Inject] private InteractionLockController LockController { get; set; }

        private List<InteractionOption> _options = new List<InteractionOption>();

        [Inject]
        private void Construct()
        {
            foreach (var factory in OptionFactories)
            {
                Container.Inject(factory);
            }
        }
        
        public void Interact(GameObject interactor)
        {
            _options.Clear();
            _options.AddRange(DefaultOptions);
            
            foreach (var factory in OptionFactories)
            {
                _options.AddRange(factory.Generate(gameObject));
            }

            foreach (var option in _options)
            {
                Container.Inject(option);
            }
            
            var ctx = new InteractionContext(interactor, gameObject);

            var possibleInteractions = _options
                .Where(x => x.Validate(ctx))
                .ToArray();

            if (possibleInteractions.Length > 1)
            {
                Controller.NewInteraction(interactor, gameObject, possibleInteractions);
            }
            else
            {
                
            }
            
            LockController.AcquireLock(InteractionLockType.All);
        }
    }
}