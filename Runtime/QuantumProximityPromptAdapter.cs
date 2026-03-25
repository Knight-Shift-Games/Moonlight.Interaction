using Moonlight.Backend;
using Moonlight.Quantum;
using Quantum;
using R3;
using Zenject;

namespace Moonlight.Interaction
{
    public sealed class QuantumProximityPromptAdapter : QuantumEntityViewComponent
    {
        [Inject] private LazyInject<QuantumEntityViewUpdater> _evu;
        private ProximityPrompt _proximityPrompt;

        [Inject] private SignalBus SignalBus { get; set; }
        
        public override void OnActivate(Frame f)
        {
            _proximityPrompt = GetComponent<ProximityPrompt>();
            
            if (NetworkUtil.TryGetLocalPlayerCharacter(f, out var player))
            {
                _proximityPrompt.player = _evu.Value.GetView(player).transform;
            }
            else
            {
                SignalBus.GetStream<OnPlayerJoinedSimulationSignal>()
                    .Subscribe(_ =>
                    {
                        NetworkUtil.TryGetLocalPlayerCharacter(f, out var localPlayerEntity);
                        _proximityPrompt.player = _evu.Value.GetView(localPlayerEntity).transform;
                    })
                    .AddTo(this);
            }
        }
    }
}