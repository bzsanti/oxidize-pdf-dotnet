---
description: "Crea rama release con versioning y release notes"
argument-hint: "<version>"
---

Crea una rama release desde develop para preparar la version: $ARGUMENTS

Ejecuta los siguientes pasos:

1. **Validar version**:
   - Si $ARGUMENTS esta vacio, muestra error con guia de Semantic Versioning
   - Valida formato MAJOR.MINOR.PATCH (ej: 1.2.0, 2.0.0-beta.1)
   - Si formato invalido, muestra error

2. **Verificar que no existe rama o tag**:
   - Verifica `release/$ARGUMENTS` local y remoto
   - Verifica tag `v$ARGUMENTS`
   - Si existe, muestra error

3. **Mostrar informacion de versiones**:
   - Ultimo tag existente
   - Tipo de release detectado (MAJOR/MINOR/PATCH)

4. **Verificar estado de develop**:
   - Cambiar a develop
   - Verificar que no hay cambios sin commitear
   - Actualizar develop: `git pull origin develop`

5. **Ejecutar validaciones pre-release**:
   - Tests del proyecto
   - Linting
   - Build
   - Si falla alguna validacion, DETENTE

6. **Crear la rama release**:
   - `git checkout -b release/$ARGUMENTS`

7. **Actualizar archivos de version** (si existen):
   - package.json: `npm version $ARGUMENTS --no-git-tag-version`
   - Cargo.toml: actualizar campo version
   - pyproject.toml: actualizar campo version
   - Crear archivo VERSION con la version

8. **Crear plantilla de release notes**:
   - Archivo: RELEASE-NOTES-v$ARGUMENTS.md
   - Incluir: Summary, New Features, Bug Fixes, Breaking Changes
   - Incluir changelog automatico desde ultimo tag
   - Commit los cambios

9. **Push la rama release**:
   - `git push -u origin release/$ARGUMENTS`

10. **Mostrar instrucciones finales**:

    PROXIMOS PASOS:
    1. Completa RELEASE-NOTES-v$ARGUMENTS.md
    2. Realiza pruebas finales
    3. Aplica solo bug fixes criticos

    CUANDO ESTE LISTO PARA PRODUCCION:
    1. Merge a main:
       - `git checkout main`
       - `git merge --no-ff release/$ARGUMENTS`
       - `git tag -a v$ARGUMENTS -m 'Release version $ARGUMENTS'`
       - `git push origin main --tags`
    2. Merge back a develop:
       - `git checkout develop`
       - `git merge --no-ff release/$ARGUMENTS`
       - `git push origin develop`
    3. Eliminar rama release
