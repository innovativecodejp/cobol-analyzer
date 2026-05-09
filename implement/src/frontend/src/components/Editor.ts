import * as monaco from 'monaco-editor';

export class Editor {
  private readonly inner: monaco.editor.IStandaloneCodeEditor;

  constructor(container: HTMLElement) {
    this.inner = monaco.editor.create(container, {
      value: '',
      language: 'plaintext',
      theme: 'vs',
      automaticLayout: true,
      minimap: { enabled: false },
    });

    this.inner.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyC, () => {
      const sel = this.inner.getSelection();
      const model = this.inner.getModel();
      if (!sel || !model) return;
      const text = sel.isEmpty()
        ? model.getLineContent(sel.startLineNumber)
        : model.getValueInRange(sel);
      navigator.clipboard.writeText(text).catch(() => undefined);
    });

    this.inner.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyX, () => {
      const sel = this.inner.getSelection();
      const model = this.inner.getModel();
      if (!sel || !model) return;
      const isEmpty = sel.isEmpty();
      const text = isEmpty
        ? model.getLineContent(sel.startLineNumber)
        : model.getValueInRange(sel);
      navigator.clipboard.writeText(text).then(() => {
        if (isEmpty) {
          const line = sel.startLineNumber;
          const lineCount = model.getLineCount();
          const range = line < lineCount
            ? new monaco.Range(line, 1, line + 1, 1)
            : new monaco.Range(line, 1, line, model.getLineMaxColumn(line));
          this.inner.executeEdits('cut', [{ range, text: '' }]);
        } else {
          this.inner.executeEdits('cut', [{ range: sel, text: '' }]);
        }
      }).catch(() => undefined);
    });

    this.inner.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyV, () => {
      navigator.clipboard.readText().then(text => {
        const sel = this.inner.getSelection();
        if (!sel) return;
        this.inner.executeEdits('paste', [{ range: sel, text }]);
      }).catch(() => undefined);
    });
  }

  getValue(): string {
    return this.inner.getValue();
  }

  setValue(value: string): void {
    this.inner.setValue(value);
  }
}
