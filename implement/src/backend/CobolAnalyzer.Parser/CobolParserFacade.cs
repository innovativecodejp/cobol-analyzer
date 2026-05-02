using System.IO;
using Antlr4.Runtime;
using CobolAnalyzer.Core.Ast;
using CobolAnalyzer.Core.Models;
using CobolAnalyzer.Parser.Generated;

namespace CobolAnalyzer.Parser;

public class CobolParserFacade
{
    public ParseResult Parse(string source)
    {
        if (string.IsNullOrEmpty(source))
            return new ParseResult { Errors = new List<ParseError> { new(0, 0, "Source is empty or null") } };

        var errors = new List<ParseError>();
        var listener = new CollectingErrorListener(errors);

        var inputStream = new AntlrInputStream(source);
        var lexer = new Cobol85Lexer(inputStream);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(listener);

        var tokenStream = new CommonTokenStream(lexer);
        var parser = new Cobol85Parser(tokenStream);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(listener);

        var tree = parser.startRule();

        if (errors.Count > 0)
            return new ParseResult { Errors = errors };

        var ast = new AstBuilder().Build(tree);
        return new ParseResult { Ast = ast };
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
