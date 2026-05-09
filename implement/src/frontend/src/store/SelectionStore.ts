export interface SelectionState {
  selectedAstNodeId: string | null;
  selectedAstLineRange: { start: number; end: number } | null;
  selectedCfgBlockId: string | null;
  selectedDfgNodeId: string | null;
  impactClosureIds: Set<string>;
}

type Handler = (state: SelectionState) => void;

const EMPTY: SelectionState = {
  selectedAstNodeId: null,
  selectedAstLineRange: null,
  selectedCfgBlockId: null,
  selectedDfgNodeId: null,
  impactClosureIds: new Set(),
};

class SelectionStoreImpl {
  private state: SelectionState = { ...EMPTY, impactClosureIds: new Set() };
  private readonly handlers = new Set<Handler>();

  on(handler: Handler): () => void {
    this.handlers.add(handler);
    return () => this.handlers.delete(handler);
  }

  private emit(): void {
    for (const h of this.handlers) h(this.state);
  }

  selectAstNode(nodeId: string, lineRange: { start: number; end: number }): void {
    this.state = { ...EMPTY, impactClosureIds: new Set(), selectedAstNodeId: nodeId, selectedAstLineRange: lineRange };
    this.emit();
  }

  selectCfgBlock(blockId: string): void {
    this.state = { ...EMPTY, impactClosureIds: new Set(), selectedCfgBlockId: blockId };
    this.emit();
  }

  selectDfgNode(nodeId: string, closureIds: string[]): void {
    this.state = { ...EMPTY, impactClosureIds: new Set(closureIds), selectedDfgNodeId: nodeId };
    this.emit();
  }

  clearAll(): void {
    this.state = { ...EMPTY, impactClosureIds: new Set() };
    this.emit();
  }

  getState(): SelectionState {
    return this.state;
  }
}

export const selectionStore = new SelectionStoreImpl();
