using System;

namespace VisualVersionofService.Comunications
{
    internal class Disposable
    {
        public string Name = "";
        private IDisposable IDisposable;

        public Disposable(string name, IDisposable idisposable)
        {
            Name = name;
            IDisposable = idisposable;
        }

        public void Dispose()
        {
            IDisposable.Dispose();
        }
    }
}