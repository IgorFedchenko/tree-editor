import { useEffect, useState } from 'react';
import { getChildren, getRoots, type DbNode } from './api';
import TreeNode from './TreeNode';

type Props = {
  cachedIds: number[];
  busy: boolean;
  reloadKey: number;
  onLoad: (id: number) => void;
  onError: (error: unknown) => void;
};

export default function DbTreeView({ cachedIds, busy, reloadKey, onLoad, onError }: Props) {
  const [roots, setRoots] = useState<DbNode[]>([]);
  const [children, setChildren] = useState<Record<number, DbNode[]>>({});
  const [selectedId, setSelectedId] = useState<number | null>(null);

  // reads the roots and the children of every expanded node again
  async function reload() {
    const expandedIds = Object.keys(children).map(Number);
    const [newRoots, ...newChildren] = await Promise.all([getRoots(), ...expandedIds.map(getChildren)]);
    setRoots(newRoots);
    setChildren(Object.fromEntries(expandedIds.map((id, i) => [id, newChildren[i]])));
  }

  useEffect(() => {
    reload().catch(onError);
  }, [reloadKey]);

  async function toggle(id: number) {
    if (id in children) {
      setChildren((current) => {
        const copy = { ...current };
        delete copy[id];
        return copy;
      });
      return;
    }
    try {
      const loaded = await getChildren(id);
      setChildren((current) => ({ ...current, [id]: loaded }));
    } catch (e) {
      onError(e);
    }
  }

  function renderNodes(nodes: DbNode[]) {
    return nodes.map((node) => {
      const loaded = children[node.id];
      return (
        <TreeNode
          key={node.id}
          testId={`db-node-${node.id}`}
          selected={node.id === selectedId}
          onSelect={() => setSelectedId(node.id)}
          row={
            <>
              <span className="tree-toggle">
                {node.hasChildren && (
                  <button
                    className="btn btn-light btn-sm py-0"
                    onClick={(e) => {
                      e.stopPropagation();
                      toggle(node.id);
                    }}
                  >
                    {loaded ? '−' : '+'}
                  </button>
                )}
              </span>
              <span className={node.isDeleted ? 'text-decoration-line-through opacity-50' : ''}>{node.value}</span>
            </>
          }
        >
          {loaded && renderNodes(loaded)}
        </TreeNode>
      );
    });
  }

  const canLoad = selectedId !== null && !cachedIds.includes(selectedId) && !busy;

  return (
    <div className="card" data-testid="db-view">
      <div className="card-header d-flex align-items-center">
        <h2 className="h5 mb-0 me-auto">Database</h2>
        <button className="btn btn-primary btn-sm" disabled={!canLoad} onClick={() => onLoad(selectedId!)}>
          Load to cache
        </button>
      </div>
      <div className="card-body">
        <ul className="list-unstyled mb-0">{renderNodes(roots)}</ul>
      </div>
    </div>
  );
}
