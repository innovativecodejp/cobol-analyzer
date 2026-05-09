import * as monaco from 'monaco-editor';

export class Editor {
  private readonly inner: monaco.editor.IStandaloneCodeEditor;
  private contextMenu: HTMLElement | null = null;

  constructor(container: HTMLElement) {
    this.inner = monaco.editor.create(container, {
      value: '',
      language: 'plaintext',
      theme: 'vs',
      automaticLayout: true,
      minimap: { enabled: false },
      contextmenu: false,
    });

    this.inner.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyC, () => this.copy());
    this.inner.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyX, () => this.cut());
    this.inner.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyV, () => this.paste());

    this.inner.onContextMenu(e => {
      e.event.preventDefault();
      const { clientX, clientY } = e.event.browserEvent;
      this.showContextMenu(clientX, clientY);
    });

    document.addEventListener('mousedown', e => {
      if (this.contextMenu && !this.contextMenu.contains(e.target as Node)) {
        this.hideContextMenu();
      }
    });
  }

  private copy(): void {
    const sel = this.inner.getSelection();
    const model = this.inner.getModel();
    if (!sel || !model) return;
    const text = sel.isEmpty()
      ? model.getLineContent(sel.startLineNumber)
      : model.getValueInRange(sel);
    navigator.clipboard.writeText(text).catch(() => undefined);
  }

  private cut(): void {
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
  }

  private paste(): void {
    navigator.clipboard.readText().then(text => {
      const sel = this.inner.getSelection();
      if (!sel) return;
      this.inner.executeEdits('paste', [{ range: sel, text }]);
    }).catch(() => undefined);
  }

  private showContextMenu(x: number, y: number): void {
    this.hideContextMenu();

    const menu = document.createElement('div');
    menu.style.cssText = [
      `position:fixed`, `left:${x}px`, `top:${y}px`,
      `background:#fff`, `border:1px solid #d0d0d0`, `border-radius:4px`,
      `box-shadow:0 2px 8px rgba(0,0,0,0.18)`, `padding:4px 0`,
      `z-index:9999`, `min-width:180px`, `user-select:none`,
      `font-family:sans-serif`, `font-size:13px`,
    ].join(';');

    const entries: Array<[string, string, () => void]> = [
      ['Cut',   'Ctrl+X', () => this.cut()],
      ['Copy',  'Ctrl+C', () => this.copy()],
      ['Paste', 'Ctrl+V', () => this.paste()],
    ];

    for (const [label, shortcut, action] of entries) {
      const row = document.createElement('div');
      row.style.cssText = 'display:flex;justify-content:space-between;align-items:center;padding:5px 16px;cursor:pointer;color:#333';

      const lbl = document.createElement('span');
      lbl.textContent = label;

      const kbd = document.createElement('span');
      kbd.textContent = shortcut;
      kbd.style.cssText = 'font-size:11px;color:#999;margin-left:32px';

      row.appendChild(lbl);
      row.appendChild(kbd);

      row.addEventListener('mouseenter', () => {
        row.style.background = '#0066b8';
        lbl.style.color = '#fff';
        kbd.style.color = '#cce4f7';
      });
      row.addEventListener('mouseleave', () => {
        row.style.background = '';
        lbl.style.color = '#333';
        kbd.style.color = '#999';
      });
      row.addEventListener('mousedown', e => {
        e.preventDefault();
        action();
        this.hideContextMenu();
      });

      menu.appendChild(row);
    }

    document.body.appendChild(menu);
    this.contextMenu = menu;

    const rect = menu.getBoundingClientRect();
    if (rect.right > window.innerWidth) menu.style.left = `${x - rect.width}px`;
    if (rect.bottom > window.innerHeight) menu.style.top = `${y - rect.height}px`;
  }

  private hideContextMenu(): void {
    this.contextMenu?.remove();
    this.contextMenu = null;
  }

  getValue(): string {
    return this.inner.getValue();
  }

  setValue(value: string): void {
    this.inner.setValue(value);
  }
}
