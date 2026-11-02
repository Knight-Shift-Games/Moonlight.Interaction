﻿using Moonlight.Backend;
#if QUANTUM_UNITY
using Moonlight.Quantum;
using Quantum;
#endif
using R3;
using Zenject;

namespace Moonlight.Interaction
{
#if QUANTUM_UNITY
    public sealed class QuantumProximityPromptAdapter : QuantumEntityViewComponent
    {
        private ProximityPrompt _proximityPrompt;
        private System.IDisposable _tracking;

        [Inject] private SignalBus SignalBus { get; set; }

        public override void OnActivate(Frame f)
        {
            _proximityPrompt = GetComponent<ProximityPrompt>();

            TrackLocalPlayer();
            SignalBus.GetStream<OnPlayerCharacterSpawnedSignal>()
                .Subscribe(_ => TrackLocalPlayer())
                .AddTo(this);
        }

        // Polls because a character spawned this tick has no view yet, and the spawn signal can come from a
        // neighbouring zone's simulation.
        private void TrackLocalPlayer()
        {
            _tracking?.Dispose();
            _tracking = Observable.EveryUpdate()
                .Select(_ => FindLocalPlayerView())
                .Where(view => view != null)
                .Take(1)
                .Subscribe(view =>
                {
                    _proximityPrompt.player = view.transform;
                    _proximityPrompt.UpdateBindingGlyph();
                })
                .AddTo(this);
        }

        private QuantumEntityView FindLocalPlayerView()
        {
            var game = Game;
            var f = game?.Frames.Predicted;
            var runner = game != null ? QuantumRunner.FindRunner(game) : null;
            var updater = EntityView != null ? EntityView.EntityViewUpdater : null;
            if (f == null || runner == null || updater == null
                || !NetworkUtil.TryGetLocalPlayerCharacter(runner, f, out var player) || player == EntityRef.None)
            {
                return null;
            }

            return updater.GetView(player);
        }
    }
#endif
}