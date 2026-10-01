import { useState } from 'react';
import { buildTree, hasChanges, type CachedNode, type NodeWithChildren } from './cache';
import TreeNode from './TreeNode';

type Props = {
  cache: CachedNode[];
  selectedId: number | null;
  editingId: number | null;
  busy: boolean;
  onSelect: (id: number) => void;
  onAddChild: () => void;
  onEdit: () => void;
  onSave: (value: string) => void;
  onCancel: () => void;
  onDelete: () => void;
  onApply: () => void;
};

export default function CachedTreeView(props: Props) {
  const { cache, selectedId, editingId } = props;
  const selected = cache.find((node) => node.id === selectedId);
  const canChange = selected !== undefined && !selected.isDeleted && !props.busy;
  const loadedIds = new Set(cache.map((node) => node.id));

  function renderNodes(nodes: NodeWithChildren[]) {
    return nodes.map(({ node, children }) => (
      <TreeNode
        key={node.id}
        testId={`cache-node-${node.id}`}
        selected={node.id === selectedId}
        onSelect={() => props.onSelect(node.id)}
        row={
          node.id === editingId ? (
            <ValueEditor value={node.value} onSave={props.onSave} onCancel={props.onCancel} />
          ) : (
            <>
              {node.parentId !== null && !loadedIds.has(node.parentId) && (
                <span className="me-2" title="Parent is not loaded">
                  …
                </span>
              )}
              <span className={node.isDeleted ? 'text-decoration-line-through opacity-50' : ''}>{node.value}</span>
              <Badge node={node} />
            </>
          )
        }
      >
        {children.length > 0 && renderNodes(children)}
      </TreeNode>
    ));
  }

  return (
    <div className="card" data-testid="cache-view">
      <div className="card-header d-flex align-items-center gap-2">
        <h2 className="h5 mb-0 me-auto">Cache</h2>
        <button className="btn btn-outline-secondary btn-sm" disabled={!canChange} onClick={props.onAddChild}>
          Add child
        </button>
        <button className="btn btn-outline-secondary btn-sm" disabled={!canChange} onClick={props.onEdit}>
          Edit
        </button>
        <button className="btn btn-outline-danger btn-sm" disabled={!canChange} onClick={props.onDelete}>
          Delete
        </button>
        <button className="btn btn-success btn-sm" disabled={!hasChanges(cache) || props.busy} onClick={props.onApply}>
          Apply
        </button>
      </div>
      <div className="card-body">
        {cache.length === 0 ? (
          <p className="text-muted mb-0">Cache is empty</p>
        ) : (
          <ul className="list-unstyled mb-0">{renderNodes(buildTree(cache))}</ul>
        )}
      </div>
    </div>
  );
}

function Badge({ node }: { node: CachedNode }) {
  if (node.id < 0) {
    return <span className="badge text-bg-success ms-2">new</span>;
  }
  if (!node.isChanged) {
    return null;
  }
  return node.isDeleted ? (
    <span className="badge text-bg-danger ms-2">deleted</span>
  ) : (
    <span className="badge text-bg-warning ms-2">edited</span>
  );
}

type ValueEditorProps = {
  value: string;
  onSave: (value: string) => void;
  onCancel: () => void;
};

function ValueEditor({ value, onSave, onCancel }: ValueEditorProps) {
  const [text, setText] = useState(value);
  const trimmed = text.trim();

  return (
    <form
      className="d-flex flex-grow-1 gap-1"
      onSubmit={(e) => {
        e.preventDefault();
        if (trimmed) {
          onSave(trimmed);
        }
      }}
    >
      <input
        className="form-control form-control-sm"
        value={text}
        maxLength={100}
        autoFocus
        onChange={(e) => setText(e.target.value)}
        onKeyDown={(e) => e.key === 'Escape' && onCancel()}
      />
      <button type="submit" className="btn btn-light btn-sm" disabled={!trimmed}>
        Save
      </button>
      <button type="button" className="btn btn-light btn-sm" onClick={onCancel}>
        Cancel
      </button>
    </form>
  );
}
