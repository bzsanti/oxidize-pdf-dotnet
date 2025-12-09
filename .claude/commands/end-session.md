---
description: "Finaliza sesion: tests, docs, commit, push y actualiza issues"
---

Ejecuta la siguiente secuencia de finalizacion de sesion:

1. **Ejecutar tests del proyecto** - Detecta el tipo de proyecto y ejecuta los tests apropiados:
   - Node.js: `npm test` o `yarn test`
   - Python: `pytest` o `python -m unittest discover`
   - Rust: `cargo test`
   - .NET: `dotnet test`
   - Go: `go test ./...`

2. **Verificar contexto del proyecto**:
   - Si el directorio contiene "BelowZero" -> Proyecto GitHub (usar GitHub Issues)
   - Si el directorio contiene "QE" -> Proyecto Azure DevOps (usar Azure DevOps workitems)
   - Otro -> Solo documentacion local

3. **Documentar progreso**:
   - Actualiza el archivo CLAUDE.md o el archivo de roadmap correspondiente con el estado actual
   - Incluye: rama actual, ultimo commit, estado de tests, archivos modificados

4. **Crear commit descriptivo** (si hay cambios pendientes):
   - Agrega archivos modificados relevantes
   - Crea commit con mensaje descriptivo del progreso de la sesion
   - NO incluyas archivos sensibles (.env, credentials, etc.)

5. **Push al repositorio remoto**:
   - Push a la rama actual
   - Verifica que el push fue exitoso

6. **Actualizar issues/workitems** segun el contexto detectado:
   - GitHub: Actualiza GitHub Issues relacionados con el trabajo realizado
   - Azure DevOps: Actualiza workitems correspondientes
   - Local: Solo confirma que la documentacion fue actualizada

7. **Mostrar resumen final**:
   - Tests ejecutados y resultado
   - Progreso documentado
   - Commits creados
   - Cambios subidos
   - Issues/workitems actualizados
   - Rama actual y hora de finalizacion

Ejecuta cada paso en orden. Si algun paso falla, reporta el error y continua con los siguientes pasos cuando sea posible.
