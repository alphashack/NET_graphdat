using System;
using System.Collections.Generic;
using System.Dynamic;

namespace Alphashack.Graphdat.Agent
{
    public interface IContextBuilder<T>
    {
        void Enter(string name = null, Func<T> create = null, Action<T> finish = null);
        T Leave(string name = null);
        T Done();
        T Exit();
        bool Validate();
        List<ExpandoObject> Flatten(Action<T, ExpandoObject> build = null);
    }
}
