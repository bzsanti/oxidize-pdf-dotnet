---
description: "Crea hotfix desde main/master para correcciones urgentes"
argument-hint: "<nombre-hotfix>"
---

Crea una rama hotfix desde main/master para correcciones urgentes en produccion. El nombre del hotfix es: $ARGUMENTS

Ejecuta los siguientes pasos:

1. **Validar nombre de hotfix**:
   - Si $ARGUMENTS esta vacio, muestra error: "Debes proporcionar un nombre para el hotfix"
   - Ejemplo de uso: `/gitflow-hotfix fix-payment-crash`

2. **Detectar rama principal**:
   - Busca si existe `origin/main` o `origin/master`
   - Si no encuentra ninguna, muestra error

3. **Verificar que la rama hotfix no existe**:
   - Verifica local y remoto para `hotfix/$ARGUMENTS`
   - Si existe, muestra error

4. **Verificar cambios pendientes**:
   - Advierte que los hotfixes deben partir de codigo limpio en produccion

5. **Actualizar rama principal**:
   - `git fetch origin [main|master]`
   - `git checkout [main|master]`
   - `git pull origin [main|master]`

6. **Mostrar informacion de produccion**:
   - Ultimo tag de version (si existe)
   - Ultimo commit en main/master

7. **Crear la rama hotfix**:
   - `git checkout -b hotfix/$ARGUMENTS`

8. **Push inicial**:
   - `git push -u origin hotfix/$ARGUMENTS`

9. **Mostrar instrucciones del flujo de hotfix**:

   FLUJO DE HOTFIX:
   1. Implementa la correccion minima necesaria
   2. Prueba exhaustivamente
   3. Merge a main/master:
      - `git checkout main`
      - `git merge --no-ff hotfix/$ARGUMENTS`
      - `git tag -a v[VERSION] -m 'Hotfix: $ARGUMENTS'`
      - `git push origin main --tags`
   4. Merge a develop para sincronizar:
      - `git checkout develop`
      - `git merge --no-ff hotfix/$ARGUMENTS`
      - `git push origin develop`
   5. Eliminar rama hotfix:
      - `git branch -d hotfix/$ARGUMENTS`
      - `git push origin --delete hotfix/$ARGUMENTS`

   Recuerda: Los hotfixes deben ser rapidos y minimos.
