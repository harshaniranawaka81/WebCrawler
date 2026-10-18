using System.Diagnostics;
using WebCrawler.Infrastructure.RateLimiting;

namespace WebCrawler.Tests.Infrastructure.RateLimiting;

[TestFixture]
public class CompositeRateLimiterTests
{
    [Test]
    public void Constructor_ThrowsWhenMaxConcurrentRequestsIsZero()
    {
        Assert.That(() => new CompositeRateLimiter(0, 0), Throws.ArgumentException);
    }

    [Test]
    public void Constructor_ClampsNegativeDelayToZero()
    {
        var limiter = new CompositeRateLimiter(-5, 1);

        Assert.That(limiter.DelayMs, Is.EqualTo(0));
    }

    [Test]
    public async Task WaitAsync_ConsumesSlot_AndReleaseRestoresIt()
    {
        var limiter = new CompositeRateLimiter(0, 2);

        await limiter.WaitAsync();
        Assert.That(limiter.AvailableSlots, Is.EqualTo(1));

        limiter.Release();
        Assert.That(limiter.AvailableSlots, Is.EqualTo(2));
    }

    [Test]
    public async Task WaitAsync_BlocksUntilSlotIsReleased()
    {
        var limiter = new CompositeRateLimiter(0, 1);
        await limiter.WaitAsync();

        var secondWait = limiter.WaitAsync();
        Assert.That(secondWait.IsCompleted, Is.False);

        limiter.Release();
        await secondWait.WaitAsync(TimeSpan.FromSeconds(5));
        limiter.Release();
    }

    [Test]
    public void WaitAsync_ThrowsWhenCancelledWhileWaitingForSlot()
    {
        var limiter = new CompositeRateLimiter(0, 1);
        limiter.WaitAsync().GetAwaiter().GetResult();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        Assert.That(async () => await limiter.WaitAsync(cts.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void WaitAsync_ThrowsWhenCancelledDuringDelay()
    {
        var limiter = new CompositeRateLimiter(5000, 5);
        limiter.WaitAsync().GetAwaiter().GetResult();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        Assert.That(async () => await limiter.WaitAsync(cts.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task WaitAsync_SpacesOutConcurrentCallers()
    {
        const int delayMs = 100;
        const int callers = 5;
        var limiter = new CompositeRateLimiter(delayMs, callers);

        var stopwatch = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(0, callers).Select(_ => Task.Run(() => limiter.WaitAsync())));
        stopwatch.Stop();

        // The first caller goes immediately; the remaining four are spaced by the delay.
        Assert.That(stopwatch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo((callers - 1) * delayMs - 30));
    }

    [Test]
    public async Task WaitAsync_EnforcesMinimumDelayBetweenRequests()
    {
        const int delayMs = 200;
        var limiter = new CompositeRateLimiter(delayMs, 1);

        await limiter.WaitAsync();
        limiter.Release();

        var stopwatch = Stopwatch.StartNew();
        await limiter.WaitAsync();
        stopwatch.Stop();
        limiter.Release();

        Assert.That(stopwatch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(delayMs / 2));
    }
}
