using Moonlight.Backend;
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
        [Inject] private QuantumEntityViewUpdaterProvider Provider { get; set; }
        private ProximityPrompt _proximityPrompt;

        [Inject] private SignalBus SignalBus { get; set; }
        
        public override void OnActivate(Frame f)
        {
            _proximityPrompt = GetComponent<ProximityPrompt>();
            
            if (NetworkUtil.TryGetLocalPlayerCharacter(f, out var player) && player != EntityRef.None)
            {
                _proximityPrompt.player = Provider.ViewUpdater.GetView(player).transform;
            }
            else
            {
                SignalBus.GetStream<OnPlayerCharacterSpawnedSignal>()
                    .Subscribe(_ =>
                    {
                        NetworkUtil.TryGetLocalPlayerCharacter(f, out var localPlayerEntity);
                        _proximityPrompt.player = Provider.ViewUpdater.GetView(localPlayerEntity).transform;
                        _proximityPrompt.UpdateBindingGlyph();
                    })
                    .AddTo(this);
            }
        }
    }
#endif
}