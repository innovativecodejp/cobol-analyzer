import type { CobolSource } from '../types/projectTypes';

const ACCEPTED_EXTENSIONS = ['.cbl', '.cob', '.cpy'];

export class FileDropZone {
  private sources: CobolSource[] = [];
  private fileInput!: HTMLInputElement;
  private listEl!: HTMLElement;
  private warningEl!: HTMLElement;
  private analyzeButton!: HTMLButtonElement;

  constructor(
    private readonly container: HTMLElement,
    private readonly onAnalyze: (sources: CobolSource[]) => void,
    private readonly onSourcesChange: (sources: CobolSource[]) => void,
  ) {}

  render(): void {
    this.container.innerHTML = '';

    const wrapper = document.createElement('div');
    wrapper.className = 'file-drop-zone';

    const dropArea = document.createElement('div');
    dropArea.className = 'file-drop-target';
    dropArea.textContent = 'ファイルをドロップ';
    dropArea.addEventListener('dragover', event => {
      event.preventDefault();
      dropArea.classList.add('drag-over');
    });
    dropArea.addEventListener('dragleave', () => {
      dropArea.classList.remove('drag-over');
    });
    dropArea.addEventListener('drop', event => {
      event.preventDefault();
      dropArea.classList.remove('drag-over');
      void this.addFiles(event.dataTransfer?.files);
    });

    const actions = document.createElement('div');
    actions.className = 'project-actions';

    this.fileInput = document.createElement('input');
    this.fileInput.type = 'file';
    this.fileInput.multiple = true;
    this.fileInput.accept = ACCEPTED_EXTENSIONS.join(',');
    this.fileInput.addEventListener('change', () => {
      void this.addFiles(this.fileInput.files);
      this.fileInput.value = '';
    });

    this.analyzeButton = document.createElement('button');
    this.analyzeButton.type = 'button';
    this.analyzeButton.textContent = 'Analyze Project';
    this.analyzeButton.addEventListener('click', () => this.onAnalyze([...this.sources]));

    actions.appendChild(this.fileInput);
    actions.appendChild(this.analyzeButton);

    this.warningEl = document.createElement('div');
    this.warningEl.className = 'project-status';

    this.listEl = document.createElement('div');
    this.listEl.className = 'project-file-list';

    wrapper.appendChild(dropArea);
    wrapper.appendChild(actions);
    wrapper.appendChild(this.warningEl);
    wrapper.appendChild(this.listEl);
    this.container.appendChild(wrapper);
    this.renderList();
  }

  getSources(): CobolSource[] {
    return [...this.sources];
  }

  setBusy(isBusy: boolean): void {
    this.analyzeButton.disabled = isBusy;
  }

  private async addFiles(files: FileList | null | undefined): Promise<void> {
    if (!files || files.length === 0)
      return;

    const nextSources = await Promise.all(
      Array.from(files)
        .filter(file => this.isAccepted(file.name))
        .map(async file => ({
          fileName: file.name,
          source: await file.text(),
        })),
    );

    const merged = [...this.sources];
    for (const source of nextSources) {
      const existingIndex = merged.findIndex(s => s.fileName === source.fileName);
      if (existingIndex >= 0)
        merged[existingIndex] = source;
      else
        merged.push(source);
    }

    this.sources = merged.slice(0, 50);
    this.warningEl.textContent = merged.length > 50
      ? '50ファイルを超えるため、先頭50件のみ保持しました。'
      : '';

    this.onSourcesChange(this.getSources());
    this.renderList();
  }

  private isAccepted(fileName: string): boolean {
    const lower = fileName.toLowerCase();
    return ACCEPTED_EXTENSIONS.some(ext => lower.endsWith(ext));
  }

  private renderList(): void {
    this.listEl.innerHTML = '';

    if (this.sources.length === 0) {
      const empty = document.createElement('div');
      empty.className = 'project-empty';
      empty.textContent = 'COBOLファイル未選択';
      this.listEl.appendChild(empty);
      return;
    }

    for (const [index, source] of this.sources.entries()) {
      const item = document.createElement('div');
      item.className = 'project-file-item';

      const name = document.createElement('span');
      name.textContent = source.fileName;

      const remove = document.createElement('button');
      remove.type = 'button';
      remove.textContent = 'Remove';
      remove.addEventListener('click', () => {
        this.sources.splice(index, 1);
        this.onSourcesChange(this.getSources());
        this.renderList();
      });

      item.appendChild(name);
      item.appendChild(remove);
      this.listEl.appendChild(item);
    }
  }
}
