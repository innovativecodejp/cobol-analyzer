import * as monaco from 'monaco-editor';

export class MonacoHighlighter {
  private decorationIds: string[] = [];

  constructor(private readonly editor: monaco.editor.IStandaloneCodeEditor) {}

  highlight(startLine: number, endLine: number, className: string): void {
    const model = this.editor.getModel();
    if (!model || startLine < 1) {
      this.clearAll();
      return;
    }
    this.decorationIds = this.editor.deltaDecorations(this.decorationIds, [
      {
        range: new monaco.Range(startLine, 1, endLine, 1),
        options: { isWholeLine: true, className },
      },
    ]);
  }

  clearAll(): void {
    this.decorationIds = this.editor.deltaDecorations(this.decorationIds, []);
  }
}
