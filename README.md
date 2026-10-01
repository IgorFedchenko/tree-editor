# Tree Editor with a Local Cache

A tree is stored in PostgreSQL and shown twice on one page: **Database** (read lazily from the API) and **Cache** (the nodes you loaded into the browser). Edits, new nodes and deletions stay in the cache until you press **Apply**. **Reset** brings the database and the page back to the sample data.

## Requirements

- .NET 10 SDK
- Docker (Docker Desktop, Colima or any other, the daemon must be running)
- Node.js 20.19+ or 22.12+ with npm
- PowerShell 7 (`pwsh`), only to install the browser for the Playwright tests

## Run

    dotnet run --project src/TreeEditor.AppHost

This starts PostgreSQL 18.3 in a container, the API and the Vite dev server. The first start also pulls the image and installs the npm packages, so it takes longer.

- UI: http://localhost:5173
- API: http://localhost:5180 (for example `/api/nodes/roots`)
- Aspire dashboard: http://localhost:15100

Ctrl+C stops everything and Aspire removes the container. The database has no volume: every start creates it again, the API applies the EF Core migration and adds the sample data when the table is empty.

Projects:

- `src/TreeEditor.AppHost` - Aspire host: PostgreSQL, API, UI
- `src/TreeEditor.Api` - minimal API, EF Core, migration, sample data
- `src/TreeEditor.ServiceDefaults` - Aspire defaults (health checks, telemetry)
- `src/TreeEditor.Web` - React and Bootstrap, built with Vite, `/api` is proxied to the API
- `tests/TreeEditor.Tests` - xUnit
- `tests/TreeEditor.E2E` - Playwright

## Tests

    dotnet test tests/TreeEditor.Tests

Unit tests for the node rules and API tests on a real PostgreSQL started by Testcontainers, so Docker must be running. With Colima set `DOCKER_HOST=unix://$HOME/.colima/default/docker.sock` and `TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock` first.

The Playwright tests drive the UI in Chromium and need the running application. Start it as described above and in another terminal run:

    dotnet build tests/TreeEditor.E2E
    pwsh tests/TreeEditor.E2E/bin/Debug/net10.0/playwright.ps1 install chromium
    dotnet test tests/TreeEditor.E2E

The browser is installed once. The tests open `E2E_BASE_URL` (`http://localhost:5173` by default) and reset the database before each test, so run them against a throwaway instance. If `E2E_ARTIFACTS_DIR` is set, every test saves a screenshot and a trace there.

## Database

One table, `nodes`. The tree is an adjacency list: a row knows only its parent.

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | integer | no | primary key `pk_nodes`, identity |
| `parent_id` | integer | yes | foreign key `fk_nodes_parent_id` to `nodes(id)`, no cascade; null for a root; never changes after insert |
| `value` | varchar(100) | no | trimmed, 1 to 100 characters, no zero character |
| `is_deleted` | boolean | no | default false |
| `deleted_at` | timestamptz | yes | set together with `is_deleted`, one value for a deleted subtree |

There is one more index, `ix_nodes_parent_id` on `parent_id`; the children lookups, the `hasChildren` check and the subtree delete use it. Rows are never removed except by Reset (`TRUNCATE`); deleted rows stay and are marked.

The schema comes from the EF Core migration `InitialCreate`. The value rules live in the `Node` entity, and it has no way to change the parent: `ParentId` is set only when a node is created with `AddChild`.

API:

- `GET /api/nodes/roots` - root nodes
- `GET /api/nodes/{id}/children` - direct children of a node, with `hasChildren` for each
- `GET /api/nodes/{id}` - one node with `ancestorIds`, the ids from the root down to its parent
- `POST /api/apply` - `{ created, updated, deleted }`, returns `{ newIds }`
- `POST /api/reset`

Errors are `application/problem+json`: 400 for a wrong value or request, 404 for a missing node, 409 for editing a deleted node or adding a child to it.

## Implementation notes

**Lazy loading.** The Database view asks for the roots when the page opens and for the children of a node when you expand it, one level per request. `hasChildren` (an `EXISTS` subquery) decides which rows get the expand button. Collapsing a node forgets its children, the next expand asks again. A request returns all direct children of a node, there is no paging. Deleted nodes are returned too and drawn struck through.

**Cache.** The cache is a flat list in the React state of `App` (the functions over it are in `cache.ts`), so reloading the page empties it. "Load to cache" is one `GET /api/nodes/{id}` and brings the node without its children. A node that is already cached cannot be loaded again, so pending edits are never overwritten. Besides loading a node, Apply and Reset, the cache does not call the API. New nodes get negative temporary ids (-1, -2, ...) and can have children of their own. Add child needs a selected node, so a new root cannot be created.

**Hierarchy in the cache.** Every cached node keeps `ancestorIds` from the server. The tree is built from the flat list on each render (`buildTree`): a node goes under its closest ancestor that is in the cache, or to the top level if there is none. When the direct parent is missing, the row shows a "…" marker. So nodes can be loaded in any order: when a missing ancestor is loaded later, its descendants move under it on their own. The links never change, so `ancestorIds` cannot go stale.

**Delete.** In the cache, Delete marks the node and every cached node that has it in `ancestorIds`, also those behind levels that are not loaded; new nodes in that subtree are just dropped. A node loaded later under a deleted ancestor appears already deleted. Deleted nodes cannot be edited, cannot get children and cannot be deleted again. The server has to delete descendants that were never loaded, so Apply sends the ids of the nodes deleted in the cache, and for each of them one recursive query marks the whole subtree in the database, without reading it into memory:

    WITH RECURSIVE subtree AS (
        SELECT id FROM nodes WHERE id = @id
        UNION ALL
        SELECT n.id FROM nodes n JOIN subtree ON n.parent_id = subtree.id
    )
    UPDATE nodes SET is_deleted = true, deleted_at = now()
    WHERE id IN (SELECT id FROM subtree) AND NOT is_deleted

`ancestorIds` is read with the same kind of query, going up instead of down.

**Apply.** The page sends all pending changes in one `POST /api/apply`; the badges `new`, `edited` and `deleted` show what will be sent. The API runs it in one transaction: new nodes in the order they were sent (the parent is a node from the database or one created earlier in the same request), then changed values, `SaveChanges`, then the deletions, then commit. Any error rolls everything back, the cache keeps its pending changes and the message is shown at the top of the page. After a 200 the page replaces the temporary ids with the real ones from `newIds`, clears the badges and reads the Database view again, keeping the expanded nodes. A value that was edited and then deleted before Apply is still saved first and then deleted. The Aspire PostgreSQL integration turns on the EF Core retrying execution strategy, which does not allow our own transactions, so retries are switched off in `Program.cs`.

**Reset.** `POST /api/reset` runs in one transaction: `TRUNCATE nodes RESTART IDENTITY`, insert of the sample rows with fixed ids, and moving the identity sequence so that the next new node gets id 18. The page then clears the cache, the selection, the editor and the error message, and collapses the Database view. There is no confirmation dialog.

**Sample data.** 17 nodes with ids 1 to 17, the value is the path of the node. The first tree has 6 levels (`Node 1` down to `Node 1.1.1.1.1.1`), the second has 4 (`Node 2` down to `Node 2.1.1.1`). The same code (`SampleData.Reset`) runs on the first start and on Reset.

**Concurrency.** Not handled on purpose: the application has one user, no row versions or locks. If the cache is stale (a node was deleted from another tab, for example), Apply gets 404 or 409, nothing is saved and Reset gets you out.
