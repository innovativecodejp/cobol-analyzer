using CobolAnalyzer.Core.Ast;

namespace CobolAnalyzer.Core.Models;

public class ParseResult
{
    public ProgramNode? Ast { get; init; }
    public List<ParseError> Errors { get; init; } = new();

    /// <summary>
    /// 非致命的な前処理由来の事象（未解決 COPY / REPLACING 非対応 / EXEC ブロック縮約 等）。
    /// 警告はパース成否（<see cref="IsSuccess"/>）に影響しない。
    /// </summary>
    public List<ParseWarning> Warnings { get; init; } = new();

    public bool IsSuccess => Errors.Count == 0;
}

public record ParseError(int Line, int Column, string Message);

/// <summary>
/// 前処理段で検出した非致命的な事象。<paramref name="Line"/> は可能な範囲での原本行の目安（不明時は 0）。
/// </summary>
public record ParseWarning(int Line, string Kind, string Message);

/// <summary>
/// <see cref="ParseWarning.Kind"/> に使う既定の分類。
/// </summary>
public static class ParseWarningKind
{
    public const string UnresolvedCopy = "UnresolvedCopy";
    public const string CopyReplacingUnsupported = "CopyReplacingUnsupported";
    public const string CopyDepthExceeded = "CopyDepthExceeded";
    public const string CopyCycle = "CopyCycle";
    public const string ExecBlockReduced = "ExecBlockReduced";
}
