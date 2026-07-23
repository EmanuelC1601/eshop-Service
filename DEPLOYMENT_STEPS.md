# Publicacion de base de datos y API

Esta solucion contiene dos APIs ASP.NET Core (`Catalog.API` y `Basket.API`), dos bases PostgreSQL y un frontend Vue Vite para catalogo de productos.

## Redis para Basket.API

`Basket.API` usa Redis como cache distribuida de los carritos. La configuracion de Docker Compose levanta Redis con persistencia AOF en el volumen `redis_data` y espera a que este saludable antes de iniciar la API.

Para ejecutar todo localmente desde la raiz de la solucion:

```text
docker compose up --build
```

Redis queda disponible para herramientas locales en `localhost:6379`; dentro de Docker, Basket usa `distributedcache:6379`. Para comprobarlo:

```text
docker compose exec distributedcache redis-cli ping
```

Debe responder `PONG`. Las llaves de carrito se guardan con el prefijo `basket:` y expiran tras 30 minutos sin importar el uso (con renovacion deslizante cada 5 minutos).

## Base de datos gratuita

Opcion recomendada para practica: Neon, Supabase o Render PostgreSQL.

1. Crear una cuenta en el proveedor elegido.
2. Crear una base PostgreSQL llamada `CatalogDb` y otra llamada `BasketDb`.
3. Copiar el connection string externo.
4. En el servicio donde se publique la API, registrar una variable:

```text
ConnectionStrings__Database=Host=HOST;Port=5432;Database=CatalogDb;Username=USER;Password=PASSWORD;SSL Mode=Require;Trust Server Certificate=true
```

5. Repetir la variable en el servicio de `Basket.API`, cambiando `Database=BasketDb`.
6. No subir contrasenas reales al repositorio.

## API en nube

Opcion simple: Render Web Service o Railway.

El proyecto incluye `render.yaml` para crear los servicios de Catalog y Basket como un Render Blueprint. Al conectarlo por primera vez, Render pedira los valores marcados como secretos (`sync: false`). Es mejor usar este archivo que crear los servicios manualmente.

1. Subir el proyecto a GitHub.
2. Crear un servicio web nuevo conectado al repositorio.
3. Configurar el directorio raiz como el folder de la solucion.
4. Para Catalog, usar Docker con `src/Catalog.API/Dockerfile`, o configurar estos comandos:

```text
Build: dotnet publish src/Catalog.API/Catalog.API.csproj -c Release -o out
Start: dotnet out/Catalog.API.dll
```

5. Agregar la variable `ConnectionStrings__Database` con el valor de la base publicada y `Cors__AllowedOrigins__0` con la URL final de Netlify (por ejemplo, `https://mi-catalogo.netlify.app`).
6. Para Basket, crear otro servicio web con `src/Basket.API/Dockerfile`, o configurar:

```text
Build: dotnet publish src/Basket.API/Basket.API.csproj -c Release -o out
Start: dotnet out/Basket.API.dll
```

7. Provisionar un Redis administrado (por ejemplo, Redis Cloud, Upstash o el servicio Redis del proveedor) y registrar en Basket la variable:

```text
ConnectionStrings__Redis=HOST:PUERTO,password=PASSWORD,ssl=true,abortConnect=false
```

Tambien registrar `Cors__AllowedOrigins__0` con la URL final de Netlify. No guardar estos valores en archivos `appsettings` ni en Git.

8. Publicar y probar Catalog:

```text
GET /products/search?pageNumber=1&pageSize=10
POST /products
PUT /products/{name}
DELETE /products/{name}
```

9. Publicar y probar Basket:

```text
GET /basket/{userName}
POST /basket
DELETE /basket/{userName}
```

## Frontend Vue Vite

El archivo `netlify.toml` ya define la carpeta base, el comando de compilacion y el directorio a publicar.

1. En Netlify, importar el repositorio.
2. En las variables de entorno de compilacion, agregar `VITE_API_BASE_URL` con la URL HTTPS de Catalog publicada en Render.
3. Desplegar. La variable no es secreta: se incorpora al JavaScript compilado y por eso solo debe contener la URL publica de la API.
4. Para probar localmente, entrar a `frontend`, crear `.env` a partir de `.env.example` y ejecutar:

```text
npm install
npm run build
```

5. Publicar la carpeta `frontend/dist` en Netlify, Vercel o GitHub Pages.
