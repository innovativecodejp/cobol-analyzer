using CobolAnalyzer.API.Controllers;
using CobolAnalyzer.Core.Models;
using CobolAnalyzer.Engine.Project;
using Microsoft.AspNetCore.Mvc;

namespace CobolAnalyzer.API.Tests;

public class ProjectControllerTests
{
    [Fact]
    public void Analyze_EmptySources_ReturnsBadRequest()
    {
        var analyzer = new FakeProjectAnalyzer();
        var controller = new ProjectController(analyzer);

        var result = controller.Analyze(new ProjectAnalyzeRequest());

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, analyzer.CallCount);
    }

    [Fact]
    public void Analyze_ExceedsMaxSources_ReturnsBadRequest()
    {
        var analyzer = new FakeProjectAnalyzer();
        var controller = new ProjectController(analyzer);
        var request = new ProjectAnalyzeRequest
        {
            Sources = Enumerable.Range(1, 51)
                .Select(i => new CobolSource($"P{i}.cbl", "source"))
                .ToList()
        };

        var result = controller.Analyze(request);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, analyzer.CallCount);
    }

    [Fact]
    public void Analyze_ValidSources_CallsProjectAnalyzer()
    {
        var analyzer = new FakeProjectAnalyzer();
        var controller = new ProjectController(analyzer);
        var request = new ProjectAnalyzeRequest
        {
            Sources = new List<CobolSource>
            {
                new("PROG-A.cbl", "source")
            }
        };

        var result = controller.Analyze(request);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, analyzer.CallCount);
    }

    private sealed class FakeProjectAnalyzer : IProjectAnalyzer
    {
        public int CallCount { get; private set; }

        public ProjectAnalyzeResult Analyze(IReadOnlyList<CobolSource> sources)
        {
            CallCount++;
            return new ProjectAnalyzeResult();
        }
    }
}
