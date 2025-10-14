using System.Collections.Generic;
using ObservableCollections;
using R3;
using TMPro;
using UnityEngine;
using Zenject;

namespace Moonlight.Interaction
{
    public sealed class InteractionView : MonoBehaviour
    {
        [field: SerializeField] private TMP_Text Title { get; set; }
        [field: SerializeField] private TMP_Text Description { get; set; }
        [field: SerializeField] private TMP_Text SubTitle { get; set; }
        [field: SerializeField] private Transform OptionViewParent { get; set; }
        [field: SerializeField] private GameObject OptionViewPrefab { get; set; }

        private List<InteractionOptionView> OptionViews { get; set; } = new();

        [Inject] private InteractableController Controller { get; set; }
        [Inject] private IPrefabFactory PrefabFactory { get; set; }

        [Inject]
        private void Construct()
        {
            Controller.Options
                      .ObserveAdd()
                      .Subscribe(e =>
                      {
                          var instance = PrefabFactory.Create(OptionViewPrefab, OptionViewParent);
                          var optionView = instance.GetComponent<InteractionOptionView>();
                          optionView.Refresh(new InteractionOptionDto(e.Value, Controller.Options.Count));
                          OptionViews.Add(optionView);
                      })
                      .AddTo(this);

            Controller.Options
                      .ObserveClear()
                      .Subscribe(e =>
                      {
                          for (var i = OptionViews.Count - 1; i >= 0; i--)
                          {
                              Destroy(OptionViews[i].gameObject);
                          }
                      })
                      .AddTo(this);
        }
    }
}