# Full-Stack Assignment: Tree Editor with a Local Cache
A database contains a hierarchy of elements arranged as a tree. The tree can be arbitrarily deep and too large to load into the application in full. Each element has a value and a parent. **Existing parent–child relationships cannot be changed.**
Use a relational database. You may choose the schema and implementation approach.
## Application
Build a web application with two tree views:
- **DBTreeView** displays the database tree without loading the entire tree at once. The user can select an element and load it into a local cache.
- **CachedTreeView** displays cached elements. Elements can be loaded individually and in any order. When separately loaded elements are related, they must automatically appear in the correct hierarchy.
For a selected element in CachedTreeView, the user must be able to:
- Edit its value.
- Add a child element.
- Delete the element.
- Apply all pending changes to the database.
Edits, additions, and deletions must remain in the cache until **Apply** is clicked. The cache may access the database only when loading an element or applying changes.
Deleting an element must also delete all its descendants, including descendants that were never loaded into the cache. Deleted elements should be marked as deleted and must no longer be available for editing.
Add a **Reset** button that restores the application and database to the initial sample data.
## Submission
Submit a link to a repository containing a **working application**. Include a `README.md` with one command or short, clear steps to start the database, API, and UI.
Provide initial sample data with **at least four levels of nesting** so the tree behavior can be tested. Briefly explain your database schema and any important implementation decisions in the README.
