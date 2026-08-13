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
- Orders API: `http://localhost:5002` (Swagger: `/swagger`)

El frontend usa ambas APIs. Para desarrollo local no necesitas variables. Para Netlify configura:

```text
VITE_API_BASE_URL=https://tu-catalog-api.onrender.com
VITE_BASKET_API_BASE_URL=https://tu-basket-api.onrender.com
VITE_ORDERS_API_BASE_URL=https://tu-orders-api.onrender.com
```

En Render, configura la URL pública de Netlify en `Cors__AllowedOrigins__0` para **los dos** servicios.

## Orders API (Fase 1)

`Orders.API` consulta por HTTP el carrito existente con `GET /basket/{userName}` y guarda una copia histórica de sus artículos en MongoDB. El cliente se identifica por el mismo `userName` del Basket: no hay login ni autenticación.

Configura secretos exclusivamente mediante variables de entorno:

```text
Services__BasketUrl=https://tu-basket-api.onrender.com
MongoDbSettings__ConnectionString=mongodb+srv://<usuario>:<password>@<cluster>/
MongoDbSettings__DatabaseName=OrdersDb
MongoDbSettings__CollectionName=Orders
```

Crear una orden:

```http
POST /api/orders
Content-Type: application/json

{ "customerId": "Emanuel", "basketId": "Emanuel" }
```

Respuesta: `201 Created` con `{ "orderId": "..." }`. Si no existe el carrito o está vacío se devuelve `400 Bad Request` y no se crea ninguna orden.

## Despliegue

- `render.yaml` define los servicios para Render.
- `netlify.toml` define la compilacion de Vue/Vite para Netlify.
- Las instrucciones y variables requeridas estan en `DEPLOYMENT_STEPS.md`.

No subir credenciales, archivos `.env`, directorios `bin`, `obj`, `node_modules`
ni configuracion local de Visual Studio.
