# Tree Editor with a Local Cache

A tree stored in PostgreSQL, two tree views in the browser (database and local cache) and an Apply button that writes the cached changes back.

## Run

You need the .NET 10 SDK, Docker and Node.js 20.19+ (or 22.12+).

    dotnet run --project src/TreeEditor.AppHost

The UI is at http://localhost:5173, the API at http://localhost:5180, the Aspire dashboard at http://localhost:15100.
