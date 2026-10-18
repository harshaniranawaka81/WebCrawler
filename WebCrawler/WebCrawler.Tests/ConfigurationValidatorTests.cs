using NUnit.Framework;
using WebCrawler.Configuration;
using WebCrawler.Core.Exceptions;
using WebCrawler.Core.Services;

namespace WebCrawler.Tests;

[TestFixture]
public class ConfigurationValidatorTests
{
    private ConfigurationValidator _validator;

    [SetUp]
    public void Setup()
    {
        _validator = new ConfigurationValidator();
    }

    [Test]
    public void Validate_ThrowsOnNullSettings()
    {
        Assert.Throws<ConfigurationException>(() => _validator.Validate(null!));
    }

    [Test]
    public void Validate_ThrowsWhenDefaultUrlIsEmpty()
    {
        var settings = new WebCrawlerSettings { DefaultUrl = "" };

        Assert.Throws<ConfigurationException>(() => _validator.Validate(settings));
    }

    [Test]
    public void Validate_ThrowsWhenDefaultUrlIsInvalid()
    {
        var settings = new WebCrawlerSettings { DefaultUrl = "not a valid url" };

        Assert.Throws<ConfigurationException>(() => _validator.Validate(settings));
    }

    [Test]
    public void Validate_AcceptsValidUrl()
    {
        var settings = new WebCrawlerSettings { DefaultUrl = "https://example.com/" };

        Assert.DoesNotThrow(() => _validator.Validate(settings));
    }

    [Test]
    public void Validate_ThrowsWhenConnectionTimeoutIsNegative()
    {
        var settings = new WebCrawlerSettings
        {
            DefaultUrl = "https://example.com/",
            ConnectionTimeout = 0
        };

        Assert.Throws<ConfigurationException>(() => _validator.Validate(settings));
    }

    [Test]
    public void Validate_ThrowsWhenMaxPagesToCrawlIsInvalid()
    {
        var settings = new WebCrawlerSettings
        {
            DefaultUrl = "https://example.com/",
            MaxPagesToCrawl = 0
        };

        Assert.Throws<ConfigurationException>(() => _validator.Validate(settings));
    }

    [Test]
    public void Validate_AllowsNullMaxPagesToCrawl()
    {
        var settings = new WebCrawlerSettings
        {
            DefaultUrl = "https://example.com/",
            MaxPagesToCrawl = null
        };

        Assert.DoesNotThrow(() => _validator.Validate(settings));
    }

    [Test]
    public void Validate_ThrowsWhenDelayIsNegative()
    {
        var settings = new WebCrawlerSettings
        {
            DefaultUrl = "https://example.com/",
            DelayBetweenRequestsMs = -1
        };

        Assert.Throws<ConfigurationException>(() => _validator.Validate(settings));
    }

    [Test]
    public void Validate_AllowsZeroDelay()
    {
        var settings = new WebCrawlerSettings
        {
            DefaultUrl = "https://example.com/",
            DelayBetweenRequestsMs = 0
        };

        Assert.DoesNotThrow(() => _validator.Validate(settings));
    }

    [Test]
    public void Validate_ThrowsWhenMaxConcurrentRequestsIsInvalid()
    {
        var settings = new WebCrawlerSettings
        {
            DefaultUrl = "https://example.com/",
            MaxConcurrentRequests = 0
        };

        Assert.Throws<ConfigurationException>(() => _validator.Validate(settings));
    }

    [Test]
    public void Validate_ThrowsWhenRobotsCacheExpirationIsInvalid()
    {
        var settings = new WebCrawlerSettings
        {
            DefaultUrl = "https://example.com/",
            RobotsCacheExpirationMinutes = 0
        };

        Assert.Throws<ConfigurationException>(() => _validator.Validate(settings));
    }

    [Test]
    public void Validate_ThrowsWhenRobotsFetchTimeoutIsInvalid()
    {
        var settings = new WebCrawlerSettings
        {
            DefaultUrl = "https://example.com/",
            RobotsFetchTimeoutSeconds = 0
        };

        Assert.Throws<ConfigurationException>(() => _validator.Validate(settings));
    }

    [Test]
    public void Validate_ThrowsWhenStorageFlushIntervalIsInvalid()
    {
        var settings = new WebCrawlerSettings
        {
            DefaultUrl = "https://example.com/",
            StorageFlushIntervalLinks = 0
        };

        Assert.Throws<ConfigurationException>(() => _validator.Validate(settings));
    }

    [Test]
    public void Validate_ThrowsWhenBufferSizeIsTooSmall()
    {
        var settings = new WebCrawlerSettings
        {
            DefaultUrl = "https://example.com/",
            StorageWriterBufferSize = 100 // Less than 256
        };

        Assert.Throws<ConfigurationException>(() => _validator.Validate(settings));
    }

    [Test]
    public void Validate_AllowsDefaultSettings()
    {
        var settings = new WebCrawlerSettings();

        Assert.DoesNotThrow(() => _validator.Validate(settings));
    }

    [Test]
    public void Validate_CollectsMultipleErrors()
    {
        var settings = new WebCrawlerSettings
        {
            DefaultUrl = "",
            ConnectionTimeout = 0,
            RobotsCacheExpirationMinutes = 0
        };

        var ex = Assert.Throws<ConfigurationException>(() => _validator.Validate(settings));
        Assert.That(ex.Message, Does.Contain("DefaultUrl"));
        Assert.That(ex.Message, Does.Contain("ConnectionTimeout"));
        Assert.That(ex.Message, Does.Contain("RobotsCacheExpirationMinutes"));
    }
}
