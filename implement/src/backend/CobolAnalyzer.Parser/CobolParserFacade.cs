using System.IO;
using Antlr4.Runtime;
using CobolAnalyzer.Core.Ast;
using CobolAnalyzer.Core.Models;
using CobolAnalyzer.Parser.Generated;

namespace CobolAnalyzer.Parser;

public class CobolParserFacade
{
    private readonly CobolPreprocessor _preprocessor;

    public CobolParserFacade()
        : this(new CobolPreprocessorOptions())
    {
    }

    /// <summary>
    /// コピーブック検索パス等を注入して生成する（仕様 §5）。
    /// 未指定（既定コンストラクタ）でも動作し、その場合 COPY は未解決警告扱いになる。
    /// </summary>
    public CobolParserFacade(CobolPreprocessorOptions options)
        => _preprocessor = new CobolPreprocessor(options);

    public ParseResult Parse(string source)
    {
        if (string.IsNullOrEmpty(source))
            return new ParseResult { Errors = new List<ParseError> { new(0, 0, "Source is empty or null") } };

        // 前処理（固定形式正規化・旧式 ID 段落除去・COPY 展開・EXEC 縮約）を先頭で適用（仕様 §4）
        var pre = _preprocessor.Process(source);
        var warnings = pre.Warnings;

        var errors = new List<ParseError>();
        var listener = new CollectingErrorListener(errors);

        var inputStream = new AntlrInputStream(pre.Text);
        var lexer = new Cobol85Lexer(inputStream);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(listener);

        var tokenStream = new CommonTokenStream(lexer);
        var parser = new Cobol85Parser(tokenStream);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(listener);

        var tree = parser.startRule();

        if (errors.Count > 0)
            return new ParseResult { Errors = errors, Warnings = warnings };

        var ast = new AstBuilder().Build(tree);
        return new ParseResult { Ast = ast, Warnings = warnings };
    }

    private sealed class CollectingErrorListener : BaseErrorListener, IAntlrErrorListener<int>
    {
        private readonly List<ParseError> _errors;
        public CollectingErrorListener(List<ParseError> errors) => _errors = errors;

        public override void SyntaxError(
            TextWriter output, IRecognizer recognizer,
            IToken offendingSymbol, int line, int charPositionInLine,
            string msg, RecognitionException e)
            => _errors.Add(new ParseError(line, charPositionInLine, msg));

        void IAntlrErrorListener<int>.SyntaxError(
            TextWriter output, IRecognizer recognizer,
            int offendingSymbol, int line, int charPositionInLine,
            string msg, RecognitionException e)
            => _errors.Add(new ParseError(line, charPositionInLine, msg));
    }
}
