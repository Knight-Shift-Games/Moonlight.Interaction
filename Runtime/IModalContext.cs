using System;

namespace Moonlight.Interaction
{
    public interface IModalContext<T>
    {
        Action Confirm { get; set; }
        Action Decline { get; set; }
        T Data { get; set; }
    }
}