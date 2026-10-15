# CONTRIBUTING — publicar y versionar CursorRotator

Guía para el mantenedor (tú). El usuario final solo ve `README.md`.

## Tests

Antes de tocar nada: verdes. La solución `CursorRotator.slnx` agrupa la app
y la batería xUnit de `tests\CursorRotator.Tests` (lógica de Core: parser
`.inf`, endurecimiento/seguridad, `SchemeStore`, migraciones de
`AppSettings`, cola del rotador, instalador).

```powershell
dotnet test CursorRotator.slnx -c Release --nologo -warnaserror
```

El CI (`.github\workflows\ci.yml`) corre lo mismo en `windows-latest` +
`dotnet publish` en cada push/PR. Cualquier test que toque el registro usa
nombres únicos y limpia solo lo suyo; no deja esquemas ni cursores activos.

## Publicar una versión

1. Sube `<Version>` en `src\CursorRotator.csproj` (hoy: `2.5.2`).
2. Genera el paquete y el zip del release:
   ```powershell
   powershell -ExecutionPolicy Bypass -File Empaquetar.ps1
   powershell -ExecutionPolicy Bypass -File Publicar-Release.ps1
   ```
3. En GitHub → **Releases** → **Draft a new release**:
   - Tag: `vX.Y.Z` (igual que `<Version>`, con la `v` delante).
   - Título: lo mismo o con nombre (`v2.6.0 — Agenda con destino`).
   - Descripción: qué cambió (la app la muestra al avisar).
   - Adjunta `dist\CursorRotator-vX.Y.Z.zip` → **Publish**.
4. Comparte el enlace del release. Quien tenga la app con tu repo
   configurado (`dueño/CursorRotator` en ⚙️ Opciones Avanzadas) recibe el
   aviso solo; quien no, pone el repo una vez y listo.

## Notas

- El repo debe ser **público** para que el aviso funcione en otras
  máquinas (la API de releases sin token no ve repos privados).
- No se commitea `dist/`, `paquete/`, `bin/`, `obj/` ni `.zip/.rar`
  (ver `.gitignore`): todo se regenera con los scripts.
- El tag manda: la app considera "nueva" cualquier versión mayor a la
  instalada, así que no reutilices tags.
