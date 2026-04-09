namespace FileKeeper.Tests.Core.Models;

public class MockDisposable : IDisposable
{
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }
}