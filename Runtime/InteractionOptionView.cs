using Moonlight.Core;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Moonlight.Interaction
{
    public class InteractionOptionView : MonoBehaviour, IRefreshable<InteractionOptionDto>
    {
        [field:SerializeField] private TMP_Text DescLabel { get; set; }
        [field:SerializeField] private TMP_Text KeyLabel { get; set; }
        [field:SerializeField] private Image GlyphImage { get; set; }
        [field:SerializeField] private Button Button { get; set; }

        [Inject] private InteractableController Controller { get; set; }
        
        public void Refresh(InteractionOptionDto optionPayload)
        {
            var option = optionPayload.Option;
            var index = optionPayload.Index;
            DescLabel.text = option.Message.Localized();
            KeyLabel.text = index.ToString();
            GlyphImage.sprite = option.Glyph.Icon;
            GlyphImage.color = option.Glyph.Color;

            Button.OnClickAsObservable()
                .Subscribe(_ =>
                {
                    var ctx = new InteractionContext(Controller.Interactor, Controller.Interactable);
                    option.Strategy.Handle(ctx, null);
                }).AddTo(this);
        }

        public void Empty()
        {
            DescLabel.text = string.Empty;
        }
    }
}