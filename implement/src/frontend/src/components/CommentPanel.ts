import type * as monaco from 'monaco-editor';
import { insertComments, previewRemove, removeComments } from '../api/commentApi';
import { selectionStore } from '../store/SelectionStore';
import type { CommentRemoveResult } from '../types/commentTypes';

type TagOption = 'MDI' | 'REVIEW' | 'TODO' | 'NOTE' | 'CUSTOM';

export class CommentPanel {
  private rendered = false;

  private targetLineInput!: HTMLInputElement;
  private tagSelect!: HTMLSelectElement;
  private customTagInput!: HTMLInputElement;
  private valueInput!: HTMLInputElement;
  private messageInput!: HTMLInputElement;
  private patternInput!: HTMLInputElement;
  private insertStatus!: HTMLElement;
  private removeStatus!: HTMLElement;
  private previewList!: HTMLElement;

  constructor(
    private readonly container: HTMLElement,
    private readonly getSource: () => string,
    private readonly setSource: (source: string) => void,
    private readonly editor: monaco.editor.IStandaloneCodeEditor,
  ) {}

  render(): void {
    if (!this.rendered) {
      this.renderShell();
      this.rendered = true;
    }

    const selectedLine = selectionStore.getState().selectedAstLineRange?.start;
    if (selectedLine !== undefined) {
      this.targetLineInput.value = String(selectedLine);
    } else if (!this.targetLineInput.value) {
      this.targetLineInput.value = '1';
    }
  }

  private renderShell(): void {
    this.container.innerHTML = '';
    const wrapper = document.createElement('div');
    wrapper.className = 'comment-panel';

    wrapper.appendChild(this.createInsertSection());
    wrapper.appendChild(this.createRemoveSection());
    this.container.appendChild(wrapper);
  }

  private createInsertSection(): HTMLElement {
    const section = document.createElement('section');
    section.className = 'comment-section';

    const title = document.createElement('h2');
    title.textContent = '挿入';
    section.appendChild(title);

    this.targetLineInput = this.createInput('number', '1');
    this.targetLineInput.min = '1';
    this.tagSelect = document.createElement('select');
    for (const tag of ['MDI', 'REVIEW', 'TODO', 'NOTE', 'CUSTOM'] satisfies TagOption[]) {
      const option = document.createElement('option');
      option.value = tag;
      option.textContent = tag;
      this.tagSelect.appendChild(option);
    }

    this.customTagInput = this.createInput('text', 'MY-TAG');
    this.customTagInput.className = 'comment-custom-tag hidden';
    this.valueInput = this.createInput('text', 'HIGH');
    this.messageInput = this.createInput('text', '');

    section.appendChild(this.createField('挿入先行番号', this.targetLineInput));
    section.appendChild(this.createField('タグ種別', this.tagSelect));
    section.appendChild(this.createField('カスタムタグ', this.customTagInput));
    section.appendChild(this.createField('値', this.valueInput));
    section.appendChild(this.createField('メッセージ', this.messageInput));

    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = 'Insert';
    button.addEventListener('click', () => void this.handleInsert());
    section.appendChild(button);

    this.insertStatus = document.createElement('div');
    this.insertStatus.className = 'comment-status';
    section.appendChild(this.insertStatus);

    this.tagSelect.addEventListener('change', () => this.toggleCustomTag());
    this.toggleCustomTag();

    return section;
  }

  private createRemoveSection(): HTMLElement {
    const section = document.createElement('section');
    section.className = 'comment-section';

    const title = document.createElement('h2');
    title.textContent = '削除';
    section.appendChild(title);

    this.patternInput = this.createInput('text', '\\[MDI:.*?\\]');
    section.appendChild(this.createField('正規表現パターン', this.patternInput));

    const actions = document.createElement('div');
    actions.className = 'comment-actions';

    const previewButton = document.createElement('button');
    previewButton.type = 'button';
    previewButton.textContent = 'Preview';
    previewButton.addEventListener('click', () => void this.handlePreview());

    const removeButton = document.createElement('button');
    removeButton.type = 'button';
    removeButton.textContent = 'Remove';
    removeButton.addEventListener('click', () => void this.handleRemove());

    actions.appendChild(previewButton);
    actions.appendChild(removeButton);
    section.appendChild(actions);

    this.removeStatus = document.createElement('div');
    this.removeStatus.className = 'comment-status';
    section.appendChild(this.removeStatus);

    this.previewList = document.createElement('div');
    this.previewList.className = 'comment-preview';
    section.appendChild(this.previewList);

    return section;
  }

  private createField(labelText: string, input: HTMLElement): HTMLElement {
    const label = document.createElement('label');
    label.className = 'comment-field';

    const span = document.createElement('span');
    span.textContent = labelText;
    label.appendChild(span);
    label.appendChild(input);

    return label;
  }

  private createInput(type: string, value: string): HTMLInputElement {
    const input = document.createElement('input');
    input.type = type;
    input.value = value;
    return input;
  }

  private toggleCustomTag(): void {
    this.customTagInput.classList.toggle('hidden', this.tagSelect.value !== 'CUSTOM');
  }

  private selectedTag(): string {
    return this.tagSelect.value === 'CUSTOM'
      ? this.customTagInput.value.trim()
      : this.tagSelect.value;
  }

  private async handleInsert(): Promise<void> {
    this.insertStatus.textContent = '';
    const source = this.getSource();
    const targetLine = Number(this.targetLineInput.value);
    const currentLine = this.editor.getPosition()?.lineNumber ?? targetLine;

    try {
      const result = await insertComments({
        source,
        insertions: [{
          targetLine,
          tag: this.selectedTag(),
          value: this.valueInput.value.trim(),
          message: this.messageInput.value,
        }],
      });

      this.setSource(result.source);
      this.editor.revealLineInCenter(Math.max(1, currentLine));
      const warnings = result.warnings.map(w => `行 ${w.line}: ${w.message}`).join(' / ');
      this.insertStatus.textContent = warnings
        ? `挿入しました。${warnings}`
        : '挿入しました。再分析は Analyze を押してください。';
    } catch (err) {
      this.insertStatus.textContent = err instanceof Error ? err.message : String(err);
    }
  }

  private async handlePreview(): Promise<void> {
    this.removeStatus.textContent = '';
    this.previewList.innerHTML = '';

    try {
      const result = await previewRemove({
        source: this.getSource(),
        pattern: this.patternInput.value,
      });
      this.renderRemoveResult(result, false);
    } catch (err) {
      this.removeStatus.textContent = err instanceof Error ? err.message : String(err);
    }
  }

  private async handleRemove(): Promise<void> {
    this.removeStatus.textContent = '';
    const currentLine = this.editor.getPosition()?.lineNumber ?? 1;

    try {
      const result = await removeComments({
        source: this.getSource(),
        pattern: this.patternInput.value,
      });
      if (!result.patternError) {
        this.setSource(result.source);
        this.editor.revealLineInCenter(Math.max(1, currentLine));
      }
      this.renderRemoveResult(result, true);
    } catch (err) {
      this.removeStatus.textContent = err instanceof Error ? err.message : String(err);
    }
  }

  private renderRemoveResult(result: CommentRemoveResult, removed: boolean): void {
    this.previewList.innerHTML = '';

    if (result.patternError) {
      this.removeStatus.textContent = result.patternError;
      return;
    }

    this.removeStatus.textContent = removed
      ? `${result.removedCount} 件を削除しました。再分析は Analyze を押してください。`
      : `合計 ${result.removedCount} 件が削除されます。`;

    const list = document.createElement('ul');
    for (const line of result.removedLines) {
      const item = document.createElement('li');
      item.textContent = `行 ${line.lineNumber}: ${line.content}`;
      list.appendChild(item);
    }
    this.previewList.appendChild(list);
  }
}
