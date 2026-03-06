---
description: "Verifica estado de rama respecto a develop"
argument-hint: "[rama-opcional]"
---

Verifica el estado de la rama respecto a develop. Si se proporciona $ARGUMENTS, usa esa rama; si no, usa la rama actual.

Ejecuta los siguientes pasos:

1. **Determinar rama a verificar**:
   - Si hay argumento: usar $ARGUMENTS
   - Si no: usar la rama actual con `git branch --show-current`

2. **Mostrar informacion de la rama**:
   - Nombre de la rama
   - Ultimo commit (hash y mensaje)

3. **Actualizar informacion remota**:
   - Ejecuta `git fetch origin develop`

4. **Comparar con develop**:
   - Cuenta commits detras de develop: `git rev-list --count [rama]..origin/develop`
   - Cuenta commits adelante de develop: `git rev-list --count origin/develop..[rama]`
   - Si esta detras, muestra los commits que faltan

5. **Mostrar archivos modificados**:
   - Lista archivos cambiados respecto a develop: `git diff --name-status origin/develop...[rama]`

6. **Verificar working directory**:
   - Verifica si hay cambios sin commitear
   - Si hay cambios, muestra cuales son

7. **Ejecutar validaciones del proyecto** (detecta automaticamente el tipo):
   - **Rust**: `cargo test`, `cargo clippy -- -D warnings`, `cargo fmt --check`
   - **Node.js**: `npm test`, `npm run lint`, `npm run build`
   - **Python**: `pytest`, `ruff check .` o `flake8`
   - **.NET**: `dotnet test`, `dotnet format --verify-no-changes`, `dotnet build`
   - **Go**: `go test ./...`, `go vet ./...`, `go build ./...`
   - **Java Maven**: `mvn test`, `mvn checkstyle:check`
   - **Java Gradle**: `./gradlew test`, `./gradlew check`

8. **Mostrar resumen**:
   - Si commits detras = 0 Y working directory limpio Y validaciones pasan: "La rama esta lista para merge"
   - Si no: lista los problemas que necesitan atencion antes del merge

Este comando es SOLO de verificacion. NO hace cambios en el repositorio.
