import { useState } from 'react';
import { applyChanges, getNode, reset } from './api';
import { addChild, addLoaded, changeValue, deleteNode, getChanges, replaceTempIds, type CachedNode } from './cache';
import CachedTreeView from './CachedTreeView';
import DbTreeView from './DbTreeView';

export default function App() {
  const [cache, setCache] = useState<CachedNode[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [nextTempId, setNextTempId] = useState(-1);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // the key rebuilds the database tree, reloadKey makes it read the data again
  const [dbKey, setDbKey] = useState(0);
  const [reloadKey, setReloadKey] = useState(0);

  function showError(e: unknown) {
    setError(e instanceof Error ? e.message : String(e));
  }

  // the cache must not change while a request is running, otherwise the response does not fit it
  async function run(request: () => Promise<void>) {
    setBusy(true);
    try {
      await request();
    } catch (e) {
      showError(e);
    } finally {
      setBusy(false);
    }
  }

  function loadToCache(id: number) {
    return run(async () => {
      const node = await getNode(id);
      setCache((current) => addLoaded(current, node));
    });
  }

  function select(id: number) {
    setSelectedId(id);
    if (id !== editingId) {
      setEditingId(null);
    }
  }

  function add() {
    setCache(addChild(cache, selectedId!, nextTempId));
    setSelectedId(nextTempId);
    setEditingId(null);
    setNextTempId(nextTempId - 1);
  }

  function save(value: string) {
    setCache(changeValue(cache, editingId!, value));
    setEditingId(null);
  }

  function remove() {
    const newCache = deleteNode(cache, selectedId!);
    setCache(newCache);
    setEditingId(null);
    if (!newCache.some((node) => node.id === selectedId)) {
      setSelectedId(null);
    }
  }

  function apply() {
    setEditingId(null);
    return run(async () => {
      const { newIds } = await applyChanges(getChanges(cache));
      setCache(replaceTempIds(cache, newIds));
      setSelectedId((current) => newIds.find((x) => x.tempId === current)?.id ?? current);
      setError(null);
      setReloadKey(reloadKey + 1);
    });
  }

  function resetAll() {
    return run(async () => {
      await reset();
      setCache([]);
      setSelectedId(null);
      setEditingId(null);
      setNextTempId(-1);
      setError(null);
      setDbKey(dbKey + 1);
    });
  }

  return (
    <div className="container-fluid py-3">
      <div className="d-flex align-items-center mb-3">
        <h1 className="h3 mb-0 me-auto">Tree Editor</h1>
        <button className="btn btn-outline-danger" disabled={busy} onClick={resetAll}>
          Reset
        </button>
      </div>

      {error && (
        <div className="alert alert-danger alert-dismissible" role="alert">
          {error}
          <button type="button" className="btn-close" aria-label="Close" onClick={() => setError(null)} />
        </div>
      )}

      <div className="row g-3">
        <div className="col-md-6">
          <DbTreeView
            key={dbKey}
            cachedIds={cache.map((node) => node.id)}
            busy={busy}
            reloadKey={reloadKey}
            onLoad={loadToCache}
            onError={showError}
          />
        </div>
        <div className="col-md-6">
          <CachedTreeView
            cache={cache}
            selectedId={selectedId}
            editingId={editingId}
            busy={busy}
            onSelect={select}
            onAddChild={add}
            onEdit={() => setEditingId(selectedId)}
            onSave={save}
            onCancel={() => setEditingId(null)}
            onDelete={remove}
            onApply={apply}
          />
        </div>
      </div>
    </div>
  );
}
