using WebCrawler.Infrastructure.Robots;

namespace WebCrawler.Tests.Infrastructure.Robots;

[TestFixture]
public class RobotsParserTests
{
    [Test]
    public void IsPathAllowed_BlocksDisallowedPath()
    {
        var rules = RobotsParser.Parse("User-agent: *\nDisallow: /private");

        Assert.That(RobotsParser.IsPathAllowed(rules, "/private/page"), Is.False);
    }

    [Test]
    public void IsPathAllowed_AllowsUnlistedPath()
    {
        var rules = RobotsParser.Parse("User-agent: *\nDisallow: /private");

        Assert.That(RobotsParser.IsPathAllowed(rules, "/public"), Is.True);
    }

    [Test]
    public void IsPathAllowed_AllowsAllWhenContentIsEmpty()
    {
        var rules = RobotsParser.Parse(string.Empty);

        Assert.That(RobotsParser.IsPathAllowed(rules, "/anything"), Is.True);
    }
}
