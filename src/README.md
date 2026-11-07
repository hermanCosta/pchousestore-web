# Backend modules and layers

The solution starts as a modular monolith. Business modules will be introduced only from approved specifications.

- `PcHouseStore.Domain` has no project dependencies.
- `PcHouseStore.Application` depends on Domain and will own use cases and published module contracts.
- `PcHouseStore.Infrastructure` depends on Application and Domain and owns EF Core, module entity configurations and external adapters.
- `PcHouseStore.Api` depends on Application and Infrastructure and is the HTTP/composition root.

Each capability module owns its use cases, domain rules and tables. Cross-module calls go through published Application contracts; modules do not access another module's tables directly. The initial persistence boundary is one MySQL database and one `PcHouseStoreDbContext`.
