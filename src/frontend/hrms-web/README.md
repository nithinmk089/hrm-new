# HRMS Web

Angular 21 standalone frontend for HRMS V1.

## Run locally

Requires Node.js 22.12+ and npm. From this directory:

```powershell
npm install
npm start
```

Open http://localhost:4200. Run `npm run build` and `npm test` for verification.

## Structure

- `src/app/core`: application-wide configuration and cross-cutting services.
- `src/app/layout`: responsive application shell and navigation.
- `src/app/shared`: reusable presentation primitives.
- `src/app/features`: lazy-loaded business feature routes.
- `src/environments`: environment-specific configuration.

UI-01 establishes the shell and route placeholders only. Authentication is UI-02; business screens follow the implementation plan.