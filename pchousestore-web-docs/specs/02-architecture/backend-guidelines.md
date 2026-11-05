# Backend Guidelines

**Status:** DRAFT

- Target .NET 10 and current supported C# language version.
- ASP.NET Core Web API.
- EF Core with MySQL provider selected after compatibility verification.
- Use dependency injection and options pattern.
- Keep controllers/endpoints thin.
- Put orchestration in Application and invariants in Domain.
- Use DTOs; never serialize EF entities as public API contracts.
- Use decimal for financial values.
- Use CancellationToken on asynchronous I/O.
- Use transactions for multi-aggregate workflows where atomicity is required.
- Use optimistic concurrency where concurrent updates are possible.
- Centralize exception handling and return Problem Details.
- Validate input at API boundary and enforce domain invariants independently.
- Use structured logging with sensitive-data redaction.
- Add unit tests for domain/application behavior and integration tests for persistence/API.
- Document contracts through OpenAPI.
- Do not introduce MediatR, AutoMapper, generic repositories or Unit of Work by default; justify each dependency/pattern.
