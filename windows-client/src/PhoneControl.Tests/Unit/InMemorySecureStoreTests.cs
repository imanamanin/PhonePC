using FluentAssertions;
using PhoneControl.Infrastructure;

namespace PhoneControl.Tests.Unit;

public sealed class InMemorySecureStoreTests
{
    [Fact]
    public async Task SaveLoadDelete_RoundTrip()
    {
        var store = new InMemorySecureStore();
        await store.SaveAsync("pairing", new byte[] { 1, 2, 3 }, CancellationToken.None);
        var loaded = await store.LoadAsync("pairing", CancellationToken.None);
        loaded.Should().Equal(1, 2, 3);
        await store.DeleteAsync("pairing", CancellationToken.None);
        (await store.LoadAsync("pairing", CancellationToken.None)).Should().BeNull();
    }
}
