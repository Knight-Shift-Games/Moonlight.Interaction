using Moonlight.Core;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Moonlight.Interaction
{
    public abstract class ModalView<TContext, TData> 
        : MonoBehaviour, 
          IRefreshable<TContext> where TContext : IModalContext<TData>
    {
        [field: SerializeField] protected Button ConfirmButton { get; set; }
        [field: SerializeField] protected Button DeclineButton { get; set; }

        public virtual void Refresh(TContext target)
        {
            ConfirmButton.OnClickAsObservable()
                         .Subscribe(_ =>
                         {
                             target.Confirm?.Invoke();
                             Destroy(gameObject);
                         }).AddTo(this);
            
            DeclineButton.OnClickAsObservable()
                         .Subscribe(_ =>
                         {
                             target.Decline?.Invoke();
                             Destroy(gameObject);
                         }).AddTo(this);
        }

        public virtual void Empty()
        {
            // Do nothing
        }
    }
}