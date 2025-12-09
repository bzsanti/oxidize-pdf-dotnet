---
description: "Crea nueva rama feature desde develop actualizado"
argument-hint: "<nombre-feature>"
---

Crea una nueva rama feature desde develop actualizado. El nombre de la feature es: $ARGUMENTS

Ejecuta los siguientes pasos:

1. **Validar nombre de feature**:
   - Si $ARGUMENTS esta vacio, muestra error: "Debes proporcionar un nombre para la feature"
   - Ejemplo de uso: `/gitflow-feature nombre-de-mi-feature`

2. **Verificar que la rama no existe**:
   - Verifica local: `git show-ref --verify refs/heads/feature/$ARGUMENTS`
   - Verifica remoto: `git show-ref --verify refs/remotes/origin/feature/$ARGUMENTS`
   - Si existe, muestra error y sugiere: `git checkout feature/$ARGUMENTS`

3. **Verificar cambios pendientes**:
   - Si hay cambios sin commitear en la rama actual, advierte que no se moveran a la nueva rama
   - Pregunta si desea continuar

4. **Actualizar develop**:
   - `git fetch origin develop`
   - `git checkout develop`
   - `git pull origin develop`
   - Si falla, detente con error

5. **Crear la nueva rama**:
   - `git checkout -b feature/$ARGUMENTS`

6. **Push inicial al remoto**:
   - `git push -u origin feature/$ARGUMENTS`

7. **Mostrar confirmacion**:
   - Rama creada: feature/$ARGUMENTS
   - Tracking: origin/feature/$ARGUMENTS

   Siguientes pasos sugeridos:
   1. Implementa tu feature
   2. Haz commits frecuentes con mensajes descriptivos
   3. Cuando este lista, usa: /gitflow-check
   4. Para merge a develop: /gitflow-merge

   Tips:
   - Mantener la rama actualizada: `git pull origin develop`
   - Usar commits semanticos: feat:, fix:, docs:, etc.
