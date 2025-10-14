using Moonlight.Core;
using Moonlight.UI;
using ObservableCollections;
using UnityEngine;
using Zenject;

namespace Moonlight.Interaction
{
    public sealed class InteractableController
    {
        public ObservableList<InteractionOption> Options { get; set; }
        public GameObject Interactable { get; set; }
        public GameObject Interactor { get; set; }
        
        [Inject] public IUIService UIService { get; set; }
        [Inject(Id = "InteractionAddon")] public StringAsset Addon { get; set; }
        
        public InteractableController()
        {
            Options = new();
        }
        
        public void NewInteraction(GameObject interactor, GameObject interactable, params InteractionOption[] options)
        {
            Interactor = interactor;
            Interactable = interactable;
            Options.Clear();

            foreach (var interactionOption in options)
            {
                Options.Add(interactionOption);
            }

            ShowAddon();
        }

        public void ShowAddon()
        {
            UIService.OpenAddon(Addon);
        }

        public void HideAddon()
        {
            UIService.CloseAddon(Addon);
        }
    }
}