# Plan: Auto-apply EF Core Migrations on Startup

## Goal
Ensure pending EF Core migrations are applied automatically when the app starts, preventing `relation "X" does not exist` errors.

## Context
- **File to change**: `Tourist_Project_MVC/Program.cs`
- **Insertion point**: Between `var app = builder.Build();` and `DbInitializer.Initialize(app.Services);`
- **Existing pattern**: `DbInitializer.Initialize` catches exceptions and logs to `Console.Error.WriteLine(...)` with a `[DbInitializer]` prefix.
- **Target DB**: PostgreSQL via Npgsql (configured in `Program.cs`).
- **Constraint**: Do not modify `Services/DbInitializer.cs`.

## Implementation Steps

1. **Add a scoped migration block** in `Program.cs` immediately after `var app = builder.Build();`.

2. **Code block to insert**:
   ```csharp
   try
   {
       using (var scope = app.Services.CreateScope())
       {
           var context = scope.ServiceProvider.GetRequiredService<TouristContext>();
           context.Database.Migrate();
       }
   }
   catch (Exception ex)
   {
       Console.Error.WriteLine($"[Program] Migration failed: {ex.Message}");
   }
   ```

3. **Rationale**:
   - Runs unconditionally (dev and prod), matching the existing seeding behavior.
   - Runs before `DbInitializer.Initialize` so the schema exists when seeding queries execute.
   - On failure, logs clearly to `Console.Error` and continues startup — consistent with the `DbInitializer` error-handling style.
   - Uses `CreateScope()` / `GetRequiredService<TouristContext>()` to match the DI pattern already used in `DbInitializer.InitializeAsync`.

## Validation
- Confirm `Program.cs` contains the new `try/catch` block between lines 120 and 125.
- Run `dotnet build` from `Tourist_Project_MVC/` to ensure compilation succeeds.
- Run the app in a test environment where pending migrations exist and verify startup logs show migration application (or a clear error message) instead of a Postgres `42P01` crash.
