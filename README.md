# eShop Service

Aplicacion de catalogo y carrito compuesta por APIs ASP.NET Core 9, PostgreSQL,
Redis y un frontend Vue 3 + Vite.

## Ejecutar localmente

```text
docker compose up --build
```

- Catalog API: `http://localhost:5000`
- Basket API: `http://localhost:5001`
- Frontend: desde `frontend`, ejecutar `npm install` y `npm run dev`

## Despliegue

- `render.yaml` define los servicios para Render.
- `netlify.toml` define la compilacion de Vue/Vite para Netlify.
- Las instrucciones y variables requeridas estan en `DEPLOYMENT_STEPS.md`.

No subir credenciales, archivos `.env`, directorios `bin`, `obj`, `node_modules`
ni configuracion local de Visual Studio.
