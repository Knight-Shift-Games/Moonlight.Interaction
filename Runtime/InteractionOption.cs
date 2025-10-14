using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Moonlight.Localization;
using UnityEngine;
using Zenject;

namespace Moonlight.Interaction
{
    [Serializable]
    public sealed class InteractionOption
    {
        [field: SerializeReference] public L10nString Message { get; set; }
        [field: SerializeField] public InteractionGlyph Glyph { get; set; }
        [field: SerializeReference] public List<IInteractionValidation> Validators { get; set; }

        [field: SerializeReference]
        [field: Required]
        public IInteractionStrategy Strategy { get; set; }

        public InteractionOption()
        {
            Message = new L10nString();
            Validators = new List<IInteractionValidation>();
        }

        public InteractionOption(L10nString message, InteractionGlyph glyph, List<IInteractionValidation> validators, IInteractionStrategy strategy)
        {
            Message = message;
            Glyph = glyph;
            Validators = validators;
            Strategy = strategy;
        }

        public bool Validate(InteractionContext ctx)
        {
            return Validators.TrueForAll(x => x.Validate(ctx, null).Success());
        }

        [Inject]
        private void Construct(DiContainer container)
        {
            container.Inject(Strategy);
        }
    }
}