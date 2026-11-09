using TechnicalTest.Api.Clients;
using TechnicalTest.Api.Models;
using TechnicalTest.Api.Processors;
using Moq;
using NUnit.Framework;

namespace TechnicalTest.Tests.ComponentTests;

[TestFixture]
[Category("Component")]
public class PostProcessorComponentTests
{
    private Mock<IPublicApiClient> _mockApiClient = null!;
    private PostProcessor _processor = null!;

    [SetUp]
    public void Setup()
    {
        _mockApiClient = new Mock<IPublicApiClient>();
        _processor = new PostProcessor(_mockApiClient.Object);
    }

}
