# Freezer Lego Meals - NestJS

NestJS implementation of the Freezer Lego Meals API.

This implementation aims to provide feature parity with the .NET implementation while following NestJS and TypeScript best practices.

---

# Requirements

The following software is required:

| Software | Version |
|----------|---------|
| Node.js | 20 LTS or newer (22 LTS recommended) |
| npm | 10+ |
| TypeScript | 5.x |
| NestJS | 10.x |

Supported operating systems:

- Windows 11
- Linux
- macOS

---

# Install

Clone the repository:

```bash
git clone <repository-url>
cd FreezerLegoMeals
```

---

# Clean Project

```powershell
Remove-Item -Recurse -Force node_modules
Remove-Item -Force package-lock.json
```

---

Install the dependencies:

```bash
npm install
```

---

# Build

```bash
npm run build
```

---

# Run

Development:

```bash
npm run start:dev
```

The API will be available at:

```
http://localhost:3000
```

---

# Swagger

Swagger UI is available at:

```
http://localhost:3000/api/docs
```

---

# Running Tests

Run every test:

```bash
npm test
```

Run only unit tests:

```bash
npm run test:unit
```

Run only integration tests:

```bash
npm run test:integration
```

Run tests with coverage:

```bash
npm run test:cov
```

Watch mode:

```bash
npm run test:watch
```

---

# Lint

```bash
npm run lint
```

---

# Format

```bash
npm run format
```

---

# Clean

Removes generated files.

```bash
npm run clean
```

---

# Project Structure

```
src/
├── api/
│   └── WebApi.NestJS/
├── domain/
├── repositories/
└── services/

tests/
├── unit/
└── integration/
```

---

# Architecture

The implementation follows a layered architecture similar to the .NET implementation.

- Controllers expose the REST API.
- Services implement the business logic.
- Repositories provide data access.
- Domain contains the business models.
- DTOs define the API contracts.
- Dependency Injection is provided by NestJS.
- Swagger/OpenAPI documentation is automatically generated.
- Jest is used for unit and integration testing.

The goal is to maintain functional parity with the .NET implementation while keeping the code idiomatic for NestJS and TypeScript.

---

# Main Technologies

- NestJS
- TypeScript
- Jest
- Supertest
- Swagger / OpenAPI
- class-validator
- class-transformer

---

# License

MIT