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
  }

  getValue(): string {
    return this.inner.getValue();
  }

  setValue(value: string): void {
    this.inner.setValue(value);
  }
}
