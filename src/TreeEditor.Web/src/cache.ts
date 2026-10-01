import type { Changes, NewId, NodeDetails } from './api';

export type CachedNode = {
  id: number;
  parentId: number | null;
  ancestorIds: number[];
  value: string;
  isDeleted: boolean;
  isChanged: boolean;
  // the value was edited, so Apply sends it even if the node is deleted afterwards
  isEdited: boolean;
};

export type NodeWithChildren = {
  node: CachedNode;
  children: NodeWithChildren[];
};

// a node goes under its closest loaded ancestor, or to the top level if there is none
export function buildTree(cache: CachedNode[]): NodeWithChildren[] {
  const byId = new Map<number, NodeWithChildren>(cache.map((node) => [node.id, { node, children: [] }]));
  const roots: NodeWithChildren[] = [];
  for (const treeNode of byId.values()) {
    const parentId = treeNode.node.ancestorIds.findLast((id) => byId.has(id));
    if (parentId === undefined) {
      roots.push(treeNode);
    } else {
      byId.get(parentId)!.children.push(treeNode);
    }
  }
  return roots;
}

export function hasChanges(cache: CachedNode[]) {
  return cache.some((node) => node.isChanged);
}

export function addLoaded(cache: CachedNode[], loaded: NodeDetails): CachedNode[] {
  if (cache.some((node) => node.id === loaded.id)) {
    return cache;
  }
  const ancestorIsDeleted = cache.some((node) => node.isDeleted && loaded.ancestorIds.includes(node.id));
  return [
    ...cache,
    {
      id: loaded.id,
      parentId: loaded.parentId,
      ancestorIds: loaded.ancestorIds,
      value: loaded.value,
      isDeleted: loaded.isDeleted || ancestorIsDeleted,
      isChanged: !loaded.isDeleted && ancestorIsDeleted,
      isEdited: false,
    },
  ];
}

export function addChild(cache: CachedNode[], parentId: number, tempId: number): CachedNode[] {
  const parent = cache.find((node) => node.id === parentId)!;
  return [
    ...cache,
    {
      id: tempId,
      parentId,
      ancestorIds: [...parent.ancestorIds, parent.id],
      value: 'New node',
      isDeleted: false,
      isChanged: true,
      isEdited: false,
    },
  ];
}

export function changeValue(cache: CachedNode[], id: number, value: string): CachedNode[] {
  return cache.map((node) =>
    node.id === id && node.value !== value ? { ...node, value, isChanged: true, isEdited: true } : node,
  );
}

// new nodes are not in the database yet, so they just disappear
export function deleteNode(cache: CachedNode[], id: number): CachedNode[] {
  const inSubtree = (node: CachedNode) => node.id === id || node.ancestorIds.includes(id);
  return cache
    .filter((node) => !(inSubtree(node) && node.id < 0))
    .map((node) => (inSubtree(node) && !node.isDeleted ? { ...node, isDeleted: true, isChanged: true } : node));
}

export function getChanges(cache: CachedNode[]): Changes {
  return {
    created: cache
      .filter((node) => node.id < 0)
      .map((node) => ({ tempId: node.id, parentId: node.parentId!, value: node.value })),
    updated: cache.filter((node) => node.id > 0 && node.isEdited).map((node) => ({ id: node.id, value: node.value })),
    deleted: cache.filter((node) => node.id > 0 && node.isChanged && node.isDeleted).map((node) => node.id),
  };
}

export function replaceTempIds(cache: CachedNode[], newIds: NewId[]): CachedNode[] {
  const realId = (id: number) => newIds.find((x) => x.tempId === id)?.id ?? id;
  return cache.map((node) => ({
    ...node,
    id: realId(node.id),
    parentId: node.parentId === null ? null : realId(node.parentId),
    ancestorIds: node.ancestorIds.map(realId),
    isChanged: false,
    isEdited: false,
  }));
}
