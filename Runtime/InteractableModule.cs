using Moonlight.Core;
using UnityEngine;
using Zenject;

namespace Moonlight.Interaction
{
    [CreateAssetMenu(menuName = "Moonlight/Interaction/Module")]
    public sealed class InteractableModule : ScriptableObjectInstaller<InteractableModule>
    {
        [field:SerializeField] private InteractionGlyphLibrary GlyphLibrary { get; set; }
        [field:SerializeField] private StringAsset Addon { get; set; }
        
        public override void InstallBindings()
        {
            Container.Bind<StringAsset>().WithId("InteractionAddon").FromInstance(Addon).AsSingle();
            Container.Bind<InteractionGlyphLibrary>().FromInstance(GlyphLibrary).AsSingle();
            Container.BindInterfacesAndSelfTo<InteractableController>().AsSingle();
            Container.BindInterfacesAndSelfTo<InteractableService>().AsSingle();
        }
    }
}