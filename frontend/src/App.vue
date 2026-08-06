<script setup>
import { computed, onMounted, reactive, ref } from 'vue';

const CATALOG_API = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';
const BASKET_API = import.meta.env.VITE_BASKET_API_BASE_URL || 'http://localhost:5001';
const CART_USER = 'eshop-demo-user';
const fallbackImage = 'https://images.unsplash.com/photo-1523381210434-271e8be1f52b?auto=format&fit=crop&w=640&q=80';

const search = reactive({ name: '', pageNumber: 1, pageSize: 10 });
const form = reactive({ name: '', description: '', categoryText: '', imageFiles: '', price: 0 });
const products = ref([]);
const cart = ref({ id: CART_USER, userName: CART_USER, items: [] });
const editingName = ref('');
const totalCount = ref(0);
const loading = ref(false);
const saving = ref(false);
const cartLoading = ref(false);
const message = ref('');
const error = ref('');

const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / search.pageSize)));
const canGoBack = computed(() => search.pageNumber > 1);
const canGoForward = computed(() => search.pageNumber < totalPages.value);
const cartItemsCount = computed(() => cart.value.items.reduce((total, item) => total + item.quantity, 0));
const cartTotal = computed(() => cart.value.items.reduce((total, item) => total + Number(item.price) * item.quantity, 0));
const isEditing = computed(() => Boolean(editingName.value));

function categoriesFromText(value) {
  return value.split(',').map(category => category.trim()).filter(Boolean);
}

function setNotice(text) { message.value = text; error.value = ''; }
function setError(text) { error.value = text; message.value = ''; }
function resetForm() {
  form.name = '';
  form.description = '';
  form.categoryText = '';
  form.imageFiles = '';
  form.price = 0;
  editingName.value = '';
}
function imageSource(imageUrl) { return imageUrl?.trim() || fallbackImage; }
function handleImageError(event) { event.target.src = fallbackImage; }
function money(value) { return Number(value || 0).toFixed(2); }

async function requestJson(baseUrl, path, options = {}) {
  const response = await fetch(`${baseUrl}${path}`, {
    headers: { 'Content-Type': 'application/json', ...(options.headers || {}) },
    ...options
  });
  if (!response.ok) {
    const text = await response.text();
    let detail = text;
    try { detail = JSON.parse(text).detail || JSON.parse(text).title || text; } catch { /* response is plain text */ }
    throw new Error(detail || `HTTP ${response.status}`);
  }
  return response.status === 204 ? null : response.json();
}

async function loadProducts() {
  loading.value = true;
  try {
    const params = new URLSearchParams({ pageNumber: search.pageNumber, pageSize: search.pageSize });
    if (search.name.trim()) params.set('name', search.name.trim());
    const data = await requestJson(CATALOG_API, `/products/search?${params}`);
    products.value = data.products || [];
    totalCount.value = data.totalCount || 0;
    search.pageNumber = data.pageNumber || search.pageNumber;
  } catch (err) {
    setError(`No se pudieron cargar productos: ${err.message}`);
  } finally { loading.value = false; }
}

function editProduct(product) {
  editingName.value = product.name;
  form.name = product.name;
  form.description = product.description;
  form.categoryText = Array.isArray(product.category) ? product.category.join(', ') : '';
  form.imageFiles = product.imageFiles;
  form.price = product.price;
  window.scrollTo({ top: 0, behavior: 'smooth' });
}

async function submitProduct() {
  if (!form.name.trim()) return setError('El nombre del producto es obligatorio.');
  if (!Number.isFinite(Number(form.price)) || Number(form.price) < 0) return setError('El precio debe ser un número mayor o igual a cero.');
  saving.value = true;
  try {
    const payload = {
      name: form.name.trim(), description: form.description.trim(), category: categoriesFromText(form.categoryText),
      imageFiles: form.imageFiles.trim(), price: Number(form.price)
    };
    if (isEditing.value) {
      await requestJson(CATALOG_API, `/products/${encodeURIComponent(editingName.value)}`, {
        method: 'PUT', body: JSON.stringify({ ...payload, name: undefined })
      });
      setNotice('Producto actualizado correctamente.');
    } else {
      await requestJson(CATALOG_API, '/products', { method: 'POST', body: JSON.stringify(payload) });
      setNotice('Producto insertado correctamente.');
    }
    resetForm();
    await loadProducts();
  } catch (err) { setError(`No se pudo guardar el producto: ${err.message}`); }
  finally { saving.value = false; }
}

async function deleteProduct(name) {
  if (!window.confirm(`¿Eliminar "${name}"?`)) return;
  saving.value = true;
  try {
    await requestJson(CATALOG_API, `/products/${encodeURIComponent(name)}`, { method: 'DELETE' });
    if (editingName.value === name) resetForm();
    setNotice('Producto eliminado correctamente.');
    await loadProducts();
  } catch (err) { setError(`No se pudo eliminar el producto: ${err.message}`); }
  finally { saving.value = false; }
}

async function loadCart() {
  cartLoading.value = true;
  try {
    const response = await requestJson(BASKET_API, `/basket/${encodeURIComponent(CART_USER)}`);
    cart.value = response.cart || cart.value;
  } catch (err) {
    // A 404 on the first visit means that this user has not saved a cart yet.
    if (!err.message.includes('404') && !err.message.toLowerCase().includes('not found')) {
      setError(`No se pudo cargar el carrito: ${err.message}`);
    }
  } finally { cartLoading.value = false; }
}

async function saveCart(nextItems, successMessage) {
  cartLoading.value = true;
  try {
    const nextCart = { id: CART_USER, userName: CART_USER, items: nextItems };
    await requestJson(BASKET_API, '/basket', { method: 'POST', body: JSON.stringify(nextCart) });
    cart.value = nextCart;
    setNotice(successMessage);
  } catch (err) { setError(`No se pudo actualizar el carrito: ${err.message}`); }
  finally { cartLoading.value = false; }
}

function addToCart(product) {
  const current = cart.value.items.find(item => item.productId === product.id);
  const nextItems = current
    ? cart.value.items.map(item => item.productId === product.id ? { ...item, quantity: item.quantity + 1 } : item)
    : [...cart.value.items, { productId: product.id, productName: product.name, quantity: 1, color: 'Predeterminado', price: product.price }];
  return saveCart(nextItems, `${product.name} se agregó al carrito.`);
}

function changeQuantity(item, delta) {
  const nextItems = cart.value.items
    .map(current => current.productId === item.productId ? { ...current, quantity: current.quantity + delta } : current)
    .filter(current => current.quantity > 0);
  return saveCart(nextItems, 'Carrito actualizado.');
}

function removeFromCart(item) {
  return saveCart(cart.value.items.filter(current => current.productId !== item.productId), 'Producto eliminado del carrito.');
}

async function clearCart() {
  if (!cart.value.items.length || !window.confirm('¿Vaciar el carrito?')) return;
  cartLoading.value = true;
  try {
    await requestJson(BASKET_API, `/basket/${encodeURIComponent(CART_USER)}`, { method: 'DELETE' });
    cart.value = { id: CART_USER, userName: CART_USER, items: [] };
    setNotice('Carrito vaciado.');
  } catch (err) { setError(`No se pudo vaciar el carrito: ${err.message}`); }
  finally { cartLoading.value = false; }
}

function changePage(delta) { search.pageNumber += delta; loadProducts(); }
function searchFromFirstPage() { search.pageNumber = 1; loadProducts(); }
onMounted(async () => { await Promise.all([loadProducts(), loadCart()]); });
</script>

<template>
  <main class="app-shell">
    <section class="toolbar">
      <div><p class="eyebrow">Colección urbana · eShop</p><h1>Tu escaparate de moda</h1><p class="hero-copy">Administra prendas y agrega productos a un carrito persistente.</p></div>
      <div class="cart-summary"><span>🛍️ {{ cartItemsCount }} prendas</span><strong>${{ money(cartTotal) }}</strong></div>
    </section>

    <section class="workspace">
      <form class="panel editor" @submit.prevent="submitProduct">
        <div class="panel-header"><h2>{{ isEditing ? 'Editar producto' : 'Nuevo producto' }}</h2><button class="secondary" type="button" @click="resetForm">{{ isEditing ? 'Cancelar' : 'Limpiar' }}</button></div>
        <p v-if="isEditing" class="form-help">El nombre identifica al producto y no se cambia durante la edición.</p>
        <label>Nombre<input v-model="form.name" :disabled="isEditing" autocomplete="off" placeholder="Nombre del producto" /></label>
        <label>Descripción<textarea v-model="form.description" rows="4" placeholder="Descripción breve"></textarea></label>
        <label>Categorías<input v-model="form.categoryText" placeholder="Ropa, Calzado, Accesorios" /></label>
        <label>Imagen de la prenda<input v-model="form.imageFiles" type="url" placeholder="https://ejemplo.com/camisa.jpg" /></label>
        <div class="image-preview" :class="{ 'is-empty': !form.imageFiles }"><img :src="imageSource(form.imageFiles)" alt="Vista previa de la prenda" @error="handleImageError" /><span>{{ form.imageFiles ? 'Vista previa' : 'Agrega una URL para ver la prenda' }}</span></div>
        <label>Precio<input v-model.number="form.price" min="0" step="0.01" type="number" /></label>
        <button class="primary" :disabled="saving" type="submit">{{ saving ? 'Guardando...' : isEditing ? 'Guardar cambios' : 'Insertar producto' }}</button>
      </form>

      <section class="panel results">
        <div class="panel-header"><h2>Catálogo</h2><span>{{ totalCount }} resultados</span></div>
        <form class="search-row" @submit.prevent="searchFromFirstPage"><input v-model="search.name" placeholder="Buscar por nombre" /><select v-model.number="search.pageSize" @change="searchFromFirstPage"><option :value="5">5</option><option :value="10">10</option><option :value="20">20</option></select><button class="secondary" type="submit">Buscar</button></form>
        <p v-if="message" class="notice success">{{ message }}</p><p v-if="error" class="notice error">{{ error }}</p>
        <div class="product-grid">
          <p v-if="loading" class="empty-state">Cargando productos...</p><p v-else-if="products.length === 0" class="empty-state">No hay productos para mostrar.</p>
          <article v-for="product in products" v-else :key="product.id" class="product-card"><img :src="imageSource(product.imageFiles)" :alt="`Imagen de ${product.name}`" @error="handleImageError" /><div class="product-card-content"><span class="category">{{ product.category?.join(', ') || 'Sin categoría' }}</span><h3>{{ product.name }}</h3><p>{{ product.description || 'Sin descripción.' }}</p><strong>${{ money(product.price) }}</strong><div class="card-actions"><button class="primary" :disabled="cartLoading" type="button" @click="addToCart(product)">Agregar</button><button class="secondary" type="button" @click="editProduct(product)">Editar</button><button class="danger" :disabled="saving" type="button" @click="deleteProduct(product.name)">Eliminar</button></div></div></article>
        </div>
        <div class="pagination"><button class="secondary" :disabled="!canGoBack" type="button" @click="changePage(-1)">Anterior</button><span>Página {{ search.pageNumber }} de {{ totalPages }}</span><button class="secondary" :disabled="!canGoForward" type="button" @click="changePage(1)">Siguiente</button></div>
      </section>

      <aside class="panel cart-panel">
        <div class="panel-header"><h2>Carrito</h2><button class="secondary" :disabled="cartLoading || !cart.items.length" type="button" @click="clearCart">Vaciar</button></div>
        <p v-if="cartLoading" class="form-help">Actualizando carrito...</p><p v-else-if="!cart.items.length" class="empty-state">Tu carrito está vacío.</p>
        <ul v-else class="cart-list"><li v-for="item in cart.items" :key="item.productId"><div><strong>{{ item.productName }}</strong><small>${{ money(item.price) }} c/u</small></div><div class="quantity"><button class="secondary" :disabled="cartLoading" type="button" @click="changeQuantity(item, -1)">−</button><span>{{ item.quantity }}</span><button class="secondary" :disabled="cartLoading" type="button" @click="changeQuantity(item, 1)">+</button></div><button class="remove" :disabled="cartLoading" type="button" @click="removeFromCart(item)">×</button></li></ul>
        <div class="cart-total"><span>Total</span><strong>${{ money(cartTotal) }}</strong></div>
      </aside>
    </section>
  </main>
</template>
