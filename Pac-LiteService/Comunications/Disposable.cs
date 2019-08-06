using System;

namespace SNPService.Comunications
{
    internal class Disposable                           //wraper around Idisposable that includes a name for the disposable object
    {
        public string Name = "";                        //name of the object
        private IDisposable IDisposable;                //object

        public Disposable(string name, IDisposable idisposable)
        {
            Name = name;                                //just cast the values over
            IDisposable = idisposable;
        }

        public void Dispose()                           //dispose of the object
        {
            IDisposable.Dispose();
        }
    }
}