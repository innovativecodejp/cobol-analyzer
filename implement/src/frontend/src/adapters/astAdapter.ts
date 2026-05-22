import type { AstNode } from '../types/analyzeResult';

export interface AstNodeWithMeta extends Omit<AstNode, 'children'> {
  collapsed: boolean;
  children: AstNodeWithMeta[];
}

export function toD3Hierarchy(astNode: AstNode): AstNodeWithMeta {
  return {
    ...astNode,
    collapsed: astNode.category === 'Element',
    children: astNode.children.map(toD3Hierarchy),
  };
}
