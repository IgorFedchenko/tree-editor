export type DbNode = {
  id: number;
  parentId: number | null;
  value: string;
  isDeleted: boolean;
  hasChildren: boolean;
};

export type NodeDetails = {
  id: number;
  parentId: number | null;
  value: string;
  isDeleted: boolean;
  ancestorIds: number[];
};

export type Changes = {
  created: { tempId: number; parentId: number; value: string }[];
  updated: { id: number; value: string }[];
  deleted: number[];
};

export type NewId = { tempId: number; id: number };

async function send<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, init);
  if (!res.ok) {
    const problem = await res.json().catch(() => null);
    throw new Error(problem?.detail ?? `Request failed with status ${res.status}`);
  }
  return res.status === 204 ? (undefined as T) : res.json();
}

export const getRoots = () => send<DbNode[]>('/api/nodes/roots');

export const getChildren = (id: number) => send<DbNode[]>(`/api/nodes/${id}/children`);

export const getNode = (id: number) => send<NodeDetails>(`/api/nodes/${id}`);

export const applyChanges = (changes: Changes) =>
  send<{ newIds: NewId[] }>('/api/apply', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(changes),
  });

export const reset = () => send<void>('/api/reset', { method: 'POST' });
