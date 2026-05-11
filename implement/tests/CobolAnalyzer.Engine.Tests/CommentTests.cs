using CobolAnalyzer.Core.Models;
using CobolAnalyzer.Engine.Comment;

namespace CobolAnalyzer.Engine.Tests;

public class CommentTests
{
    [Fact]
    public void CommentTag_ToCobolCommentLine_CorrectFormat()
    {
        var tag = new CommentTag("MDI", "HIGH", "message");

        Assert.Equal("      * [MDI:HIGH] message", tag.ToCobolCommentLine());
    }

    [Fact]
    public void CommentTag_TryParse_ValidLine_ReturnsTag()
    {
        var tag = CommentTag.TryParse("      * [MDI:HIGH] message");

        Assert.NotNull(tag);
        Assert.Equal("MDI", tag.Tag);
        Assert.Equal("HIGH", tag.Value);
        Assert.Equal("message", tag.Message);
    }

    [Fact]
    public void CommentTag_TryParse_NonCommentLine_ReturnsNull()
    {
        Assert.Null(CommentTag.TryParse("       DISPLAY 'HELLO'."));
    }

    [Fact]
    public void CommentTag_TryParse_NoTagFormat_ReturnsNull()
    {
        Assert.Null(CommentTag.TryParse("      * plain comment"));
    }

    [Fact]
    public void Insert_SingleInsertion_LineInsertedBefore()
    {
        var source = Lines("line1", "line2", "line3", "line4", "line5");
        var result = new CommentInserter().Insert(source, new[]
        {
            new InsertionSpec(5, "MDI", "HIGH", "message")
        });

        var lines = result.Source.Split('\n');
        Assert.Equal("      * [MDI:HIGH] message", lines[4]);
        Assert.Equal("line5", lines[5]);
        Assert.Equal(1, result.InsertedCount);
    }

    [Fact]
    public void Insert_MultipleInsertions_DescendingOrder()
    {
        var source = Lines("line1", "line2", "line3", "line4", "line5");
        var result = new CommentInserter().Insert(source, new[]
        {
            new InsertionSpec(2, "NOTE", "A", "before 2"),
            new InsertionSpec(5, "NOTE", "B", "before 5")
        });

        var lines = result.Source.Split('\n');
        Assert.Equal("      * [NOTE:A] before 2", lines[1]);
        Assert.Equal("line2", lines[2]);
        Assert.Equal("      * [NOTE:B] before 5", lines[5]);
        Assert.Equal("line5", lines[6]);
    }

    [Fact]
    public void Insert_TargetLineExceedsLength_AppendsToEnd()
    {
        var source = Lines("line1", "line2");
        var result = new CommentInserter().Insert(source, new[]
        {
            new InsertionSpec(99, "TODO", "REFACTOR", "review")
        });

        Assert.EndsWith("      * [TODO:REFACTOR] review", result.Source);
    }

    [Fact]
    public void Insert_LongMessage_WarningReturned()
    {
        var source = Lines("line1");
        var result = new CommentInserter().Insert(source, new[]
        {
            new InsertionSpec(1, "NOTE", "LONG", new string('X', 80))
        });

        var warning = Assert.Single(result.Warnings);
        Assert.Equal(1, warning.Line);
        Assert.Contains("72", warning.Message);
    }

    [Fact]
    public void Remove_PatternMatchesCommentLine_Removed()
    {
        var source = Lines(
            "      * [MDI:HIGH] remove me",
            "       DISPLAY 'HELLO'.");

        var result = new CommentRemover().Remove(source, @"\[MDI:.*?\]");

        Assert.Equal(1, result.RemovedCount);
        Assert.DoesNotContain("[MDI:HIGH]", result.Source);
        Assert.Contains("DISPLAY", result.Source);
    }

    [Fact]
    public void Remove_PatternMatchesCodeLine_NotRemoved()
    {
        var source = Lines(
            "       DISPLAY '[MDI:HIGH]'.",
            "       STOP RUN.");

        var result = new CommentRemover().Remove(source, @"\[MDI:.*?\]");

        Assert.Equal(0, result.RemovedCount);
        Assert.Equal(source, result.Source);
    }

    [Fact]
    public void Preview_DoesNotModifySource()
    {
        var source = Lines(
            "      * [MDI:HIGH] remove me",
            "       STOP RUN.");

        var result = new CommentRemover().Preview(source, @"\[MDI:.*?\]");

        Assert.Equal(source, result.Source);
        Assert.Equal(1, result.RemovedCount);
        Assert.Single(result.RemovedLines);
    }

    [Fact]
    public void Remove_InvalidPattern_PatternErrorSet()
    {
        var source = "      * [MDI:HIGH] remove me";

        var result = new CommentRemover().Remove(source, "[");

        Assert.NotNull(result.PatternError);
        Assert.Equal(0, result.RemovedCount);
        Assert.Equal(source, result.Source);
    }

    [Fact]
    public void Remove_PatternTimeout_HandledGracefully()
    {
        var source = "      * " + new string('a', 30_000) + "!";

        var result = new CommentRemover(TimeSpan.FromTicks(1)).Remove(source, @"^(a+)+$");

        Assert.NotNull(result.PatternError);
        Assert.Equal(0, result.RemovedCount);
        Assert.Equal(source, result.Source);
    }

    private static string Lines(params string[] lines)
        => string.Join("\n", lines);
}
