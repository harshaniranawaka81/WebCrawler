using WebCrawler.Core.Interfaces;
using WebCrawler.Infrastructure.Filters;

namespace WebCrawler.Tests.Infrastructure.Filters;

[TestFixture]
public class RobotsTextFilterTests
{
    private static readonly Uri Base = new("https://example.com/");

    [Test]
    public async Task ShouldIncludeAsync_ReturnsTrue_WhenRobotsAllows()
    {
        var filter = new RobotsTextFilter(new FakeRobotsChecker(allowed: true));

        var result = await filter.ShouldIncludeAsync(new Uri("https://example.com/page"), Base);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task ShouldIncludeAsync_ReturnsFalse_WhenRobotsDisallows()
    {
        var filter = new RobotsTextFilter(new FakeRobotsChecker(allowed: false));

        var result = await filter.ShouldIncludeAsync(new Uri("https://example.com/private"), Base);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ShouldIncludeAsync_AllowsByDefault_WhenCheckerThrows()
    {
        var filter = new RobotsTextFilter(new FakeRobotsChecker(throwOnCheck: true));

        var result = await filter.ShouldIncludeAsync(new Uri("https://example.com/page"), Base);

        Assert.That(result, Is.True);
    }

    [Test]
    public void Constructor_ThrowsWhenRobotsCheckerIsNull()
    {
        Assert.That(() => new RobotsTextFilter(null!), Throws.ArgumentNullException);
    }

    [Test]
    public void FilterName_IsRobotsText()
    {
        var filter = new RobotsTextFilter(new FakeRobotsChecker(allowed: true));

        Assert.That(filter.FilterName, Is.EqualTo("RobotsText"));
    }

    private sealed class FakeRobotsChecker : IRobotsChecker
    {
        private readonly bool _allowed;
        private readonly bool _throwOnCheck;

        public FakeRobotsChecker(bool allowed = true, bool throwOnCheck = false)
        {
            _allowed = allowed;
            _throwOnCheck = throwOnCheck;
        }

        public Task<bool> IsAllowedAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            if (_throwOnCheck)
            {
                throw new InvalidOperationException("robots failure");
            }

            return Task.FromResult(_allowed);
        }

        public Task<object?> GetRulesAsync(Uri uri, CancellationToken cancellationToken = default) => Task.FromResult<object?>(null);

        public void ClearCache()
        {
        }
    }
}
