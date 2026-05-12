import { downloadAnnotationReport, downloadMigrationDesign } from '../api/exportApi';
import { analyzeProject } from '../api/projectApi';
import type { CobolSource, ProjectAnalyzeResult } from '../types/projectTypes';
import { DependencyGraph } from './DependencyGraph';
import { FileDropZone } from './FileDropZone';
import { RankingTable } from './RankingTable';

type ProjectView = 'graph' | 'ranking';

export class ProjectPanel {
  private rendered = false;
  private sources: CobolSource[] = [];
  private result: ProjectAnalyzeResult | null = null;
  private fileDropZone!: FileDropZone;
  private graph!: DependencyGraph;
  private rankingTable!: RankingTable;
  private graphContainer!: HTMLElement;
  private rankingContainer!: HTMLElement;
  private statusEl!: HTMLElement;
  private fileSelect!: HTMLSelectElement;
  private annotationButton!: HTMLButtonElement;
  private designButton!: HTMLButtonElement;

  constructor(private readonly container: HTMLElement) {}

  render(): void {
    if (this.rendered)
      return;

    this.renderShell();
    this.rendered = true;
  }

  private renderShell(): void {
    this.container.innerHTML = '';

    const wrapper = document.createElement('div');
    wrapper.className = 'project-panel';

    const fileZoneContainer = document.createElement('section');
    fileZoneContainer.className = 'project-section project-files';
    this.fileDropZone = new FileDropZone(
      fileZoneContainer,
      sources => void this.handleAnalyze(sources),
      sources => this.handleSourcesChange(sources),
    );
    this.fileDropZone.render();

    const toolbar = this.createToolbar();
    const viewTabs = this.createViewTabs();

    const viewArea = document.createElement('section');
    viewArea.className = 'project-view-area';

    this.graphContainer = document.createElement('div');
    this.graphContainer.className = 'project-view project-view-graph active';

    this.rankingContainer = document.createElement('div');
    this.rankingContainer.className = 'project-view project-view-ranking';

    viewArea.appendChild(this.graphContainer);
    viewArea.appendChild(this.rankingContainer);

    wrapper.appendChild(fileZoneContainer);
    wrapper.appendChild(toolbar);
    wrapper.appendChild(viewTabs);
    wrapper.appendChild(viewArea);

    this.container.appendChild(wrapper);

    this.graph = new DependencyGraph(this.graphContainer);
    this.rankingTable = new RankingTable(this.rankingContainer);
    this.updateDownloadState();
  }

  private createToolbar(): HTMLElement {
    const toolbar = document.createElement('section');
    toolbar.className = 'project-toolbar';

    this.fileSelect = document.createElement('select');
    this.fileSelect.className = 'project-file-select';

    this.annotationButton = document.createElement('button');
    this.annotationButton.type = 'button';
    this.annotationButton.textContent = '注釈レポートDL';
    this.annotationButton.addEventListener('click', () => void this.handleDownloadAnnotation());

    this.designButton = document.createElement('button');
    this.designButton.type = 'button';
    this.designButton.textContent = '移行設計書DL';
    this.designButton.addEventListener('click', () => void this.handleDownloadDesign());

    this.statusEl = document.createElement('div');
    this.statusEl.className = 'project-status';

    toolbar.appendChild(this.fileSelect);
    toolbar.appendChild(this.annotationButton);
    toolbar.appendChild(this.designButton);
    toolbar.appendChild(this.statusEl);

    return toolbar;
  }

  private createViewTabs(): HTMLElement {
    const tabs = document.createElement('section');
    tabs.className = 'project-view-tabs';

    const graphButton = this.createViewButton('依存グラフ', 'graph', true);
    const rankingButton = this.createViewButton('ランキング', 'ranking', false);

    tabs.appendChild(graphButton);
    tabs.appendChild(rankingButton);
    return tabs;
  }

  private createViewButton(label: string, view: ProjectView, active: boolean): HTMLButtonElement {
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = label;
    button.className = active ? 'active' : '';
    button.addEventListener('click', () => this.switchView(view));
    return button;
  }

  private switchView(view: ProjectView): void {
    this.container.querySelectorAll('.project-view-tabs button')
      .forEach(button => button.classList.remove('active'));
    this.container.querySelectorAll('.project-view')
      .forEach(panel => panel.classList.remove('active'));

    const index = view === 'graph' ? 0 : 1;
    this.container.querySelectorAll('.project-view-tabs button')[index]?.classList.add('active');
    (view === 'graph' ? this.graphContainer : this.rankingContainer).classList.add('active');
  }

  private handleSourcesChange(sources: CobolSource[]): void {
    this.sources = sources;
    this.result = null;
    this.statusEl.textContent = '';
    this.graph.clear();
    this.rankingTable.render([]);
    this.renderFileSelect();
    this.updateDownloadState();
  }

  private async handleAnalyze(sources: CobolSource[]): Promise<void> {
    if (sources.length === 0) {
      this.statusEl.textContent = 'ファイルを選択してください。';
      return;
    }

    this.fileDropZone.setBusy(true);
    this.statusEl.textContent = '解析中...';

    try {
      this.result = await analyzeProject(sources);
      this.graph.render(this.result.dependencyGraph);
      this.rankingTable.render(this.result.ranking.entries);
      const errors = this.result.errors.length > 0 ? ` / ${this.result.errors.length} 件の注意` : '';
      this.statusEl.textContent = `解析完了: ${this.result.programs.length} プログラム${errors}`;
      this.updateDownloadState();
    } catch (err) {
      this.result = null;
      this.statusEl.textContent = err instanceof Error ? err.message : String(err);
    } finally {
      this.fileDropZone.setBusy(false);
    }
  }

  private async handleDownloadAnnotation(): Promise<void> {
    const source = this.selectedSource();
    if (!source) {
      this.statusEl.textContent = '注釈レポート対象ファイルを選択してください。';
      return;
    }

    try {
      await downloadAnnotationReport({ fileName: source.fileName, source: source.source });
      this.statusEl.textContent = '注釈レポートを生成しました。';
    } catch (err) {
      this.statusEl.textContent = err instanceof Error ? err.message : String(err);
    }
  }

  private async handleDownloadDesign(): Promise<void> {
    if (this.sources.length === 0) {
      this.statusEl.textContent = 'ファイルを選択してください。';
      return;
    }

    try {
      await downloadMigrationDesign({ sources: this.sources });
      this.statusEl.textContent = '移行設計書を生成しました。';
    } catch (err) {
      this.statusEl.textContent = err instanceof Error ? err.message : String(err);
    }
  }

  private selectedSource(): CobolSource | null {
    const fileName = this.fileSelect.value;
    return this.sources.find(source => source.fileName === fileName) ?? this.sources[0] ?? null;
  }

  private renderFileSelect(): void {
    this.fileSelect.innerHTML = '';
    for (const source of this.sources) {
      const option = document.createElement('option');
      option.value = source.fileName;
      option.textContent = source.fileName;
      this.fileSelect.appendChild(option);
    }
  }

  private updateDownloadState(): void {
    const disabled = this.sources.length === 0;
    this.fileSelect.disabled = disabled;
    this.annotationButton.disabled = disabled;
    this.designButton.disabled = disabled;
  }
}
