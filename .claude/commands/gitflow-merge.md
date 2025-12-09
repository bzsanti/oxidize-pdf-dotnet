---
description: "Ejecuta secuencia completa de merge a develop"
argument-hint: "[rama-opcional]"
---

Ejecuta la secuencia completa de Git Flow para hacer merge a develop.

Si se proporciona $ARGUMENTS, usa esa rama. Si no, usa la rama actual.

Ejecuta los siguientes pasos EN ORDEN. Si alguno falla, DETENTE inmediatamente y muestra el error:

1. **Determinar rama a mergear**:
   - Si hay argumento: usar $ARGUMENTS
   - Si no: usar `git branch --show-current`
   - Verifica que la rama existe

2. **Verificar estado limpio**:
   - `git status --porcelain`
   - Si hay cambios sin commitear, DETENTE y avisa

3. **Actualizar repositorio**:
   - `git fetch --all`

4. **Actualizar develop**:
   - `git checkout develop`
   - `git pull origin develop`

5. **Rebase de la rama feature**:
   - `git checkout [rama]`
   - `git rebase develop`
   - Si hay conflictos, DETENTE y pide instrucciones

6. **Ejecutar validaciones del proyecto** (detecta automaticamente):
   - **Rust**: `cargo test`, `cargo clippy -- -D warnings`, `cargo fmt --check`, `cargo build --release`
   - **Node.js**: `npm test`, `npm run lint`, `npm run build`
   - **Python**: `pytest`, `ruff check .`, validacion de tipos si aplica
   - **.NET**: `dotnet test`, `dotnet format --verify-no-changes`, `dotnet build -c Release`
   - **Go**: `go test ./...`, `golangci-lint run`, `go build ./...`
   - Si alguna validacion falla, DETENTE

7. **Hacer el merge**:
   - `git checkout develop`
   - `git merge --no-ff [rama] -m "Merge branch '[rama]' into develop"`

8. **Push a remoto**:
   - `git push origin develop`

9. **Limpiar rama local**:
   - `git branch -d [rama]`

10. **Preguntar si eliminar rama remota**:
    - Si confirma: `git push origin --delete [rama]`

Resultado esperado:
- Rama mergeada a develop
- Validaciones ejecutadas exitosamente
- Cambios en remoto
- Rama local eliminada
