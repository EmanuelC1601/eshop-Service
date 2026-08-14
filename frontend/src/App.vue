<script setup>
import { computed, onMounted, reactive, ref } from 'vue';
import { jsPDF } from 'jspdf';
import { GlobalWorkerOptions, getDocument } from 'pdfjs-dist';
import pdfWorker from 'pdfjs-dist/build/pdf.worker.min.mjs?url';

GlobalWorkerOptions.workerSrc = pdfWorker;

const CATALOG_API = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';
const BASKET_API = import.meta.env.VITE_BASKET_API_BASE_URL || (import.meta.env.PROD ? 'https://eshop-basket-api-86q6.onrender.com' : 'http://localhost:5001');
const ORDERS_API = import.meta.env.VITE_ORDERS_API_BASE_URL || (import.meta.env.PROD ? 'https://eshop-orders-api-l4x7.onrender.com' : 'http://localhost:5002');
const fallbackImage = 'https://images.unsplash.com/photo-1523381210434-271e8be1f52b?auto=format&fit=crop&w=640&q=80';

const view = ref('catalog');
const products = ref([]); const totalCount = ref(0); const loading = ref(false); const saving = ref(false);
const search = reactive({ name: '', pageNumber: 1, pageSize: 8 });
const form = reactive({ name: '', description: '', categoryText: '', imageFiles: '', price: 0 });
const editingName = ref(''); const customerId = ref(localStorage.getItem('eshop-customer-id') || '');
const cart = ref({ id: '', userName: '', items: [] }); const cartLoading = ref(false);
const checkoutStage = ref('summary'); const generatedOrder = ref(null);
const orders = ref([]); const ordersLoading = ref(false); const orderIdSearch = ref(''); const selectedOrder = ref(null);
const pdfLoading = ref(false);
const productDetail = ref(null); const pendingProduct = ref(null); const customerModal = ref(false);
const message = ref(''); const error = ref('');

const isEditing = computed(() => Boolean(editingName.value));
const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / search.pageSize)));
const cartItemsCount = computed(() => cart.value.items.reduce((sum, item) => sum + item.quantity, 0));
const cartTotal = computed(() => cart.value.items.reduce((sum, item) => sum + Number(item.price) * item.quantity, 0));
const visibleTitle = computed(() => ({ catalog: 'Catálogo', admin: 'Administración', cart: 'Carrito de compras', orders: 'Mis órdenes' })[view.value]);

const money = value => Number(value || 0).toFixed(2);
const imageSource = value => value?.trim() || fallbackImage;
const handleImageError = event => { event.target.src = fallbackImage; };
const categoriesFromText = value => value.split(',').map(x => x.trim()).filter(Boolean);
const setNotice = text => { message.value = text; error.value = ''; };
const setError = text => { error.value = text; message.value = ''; };

async function requestJson(baseUrl, path, options = {}) {
  const response = await fetch(`${baseUrl}${path}`, { headers: { 'Content-Type': 'application/json', ...(options.headers || {}) }, ...options });
  if (!response.ok) {
    const text = await response.text(); let detail = text;
    try { const parsed = JSON.parse(text); detail = parsed.detail || parsed.message || parsed.title || text; } catch { /* plain response */ }
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
    products.value = data.products || []; totalCount.value = data.totalCount || 0; search.pageNumber = data.pageNumber || search.pageNumber;
  } catch (err) { setError(`No se pudieron cargar productos: ${err.message}`); }
  finally { loading.value = false; }
}

function resetForm() { Object.assign(form, { name: '', description: '', categoryText: '', imageFiles: '', price: 0 }); editingName.value = ''; }
function editProduct(product) { editingName.value = product.name; Object.assign(form, { name: product.name, description: product.description, categoryText: product.category?.join(', ') || '', imageFiles: product.imageFiles, price: product.price }); window.scrollTo({ top: 0, behavior: 'smooth' }); }
async function submitProduct() {
  if (!form.name.trim()) return setError('El nombre del producto es obligatorio.');
  saving.value = true;
  try {
    const payload = { name: form.name.trim(), description: form.description.trim(), category: categoriesFromText(form.categoryText), imageFiles: form.imageFiles.trim(), price: Number(form.price) };
    if (isEditing.value) await requestJson(CATALOG_API, `/products/${encodeURIComponent(editingName.value)}`, { method: 'PUT', body: JSON.stringify({ ...payload, name: undefined }) });
    else await requestJson(CATALOG_API, '/products', { method: 'POST', body: JSON.stringify(payload) });
    setNotice(isEditing.value ? 'Producto actualizado correctamente.' : 'Producto insertado correctamente.'); resetForm(); await loadProducts();
  } catch (err) { setError(`No se pudo guardar el producto: ${err.message}`); } finally { saving.value = false; }
}
async function deleteProduct(name) { if (!window.confirm(`¿Eliminar "${name}"?`)) return; saving.value = true; try { await requestJson(CATALOG_API, `/products/${encodeURIComponent(name)}`, { method: 'DELETE' }); if (editingName.value === name) resetForm(); setNotice('Producto eliminado correctamente.'); await loadProducts(); } catch (err) { setError(`No se pudo eliminar el producto: ${err.message}`); } finally { saving.value = false; } }

async function loadCart() {
  if (!customerId.value.trim()) return;
  cartLoading.value = true;
  try { const response = await requestJson(BASKET_API, `/basket/${encodeURIComponent(customerId.value.trim())}`); cart.value = response.cart || cart.value; }
  catch (err) { if (!err.message.includes('404') && !err.message.toLowerCase().includes('not found')) setError(`No se pudo cargar el carrito: ${err.message}`); }
  finally { cartLoading.value = false; }
}
async function saveCart(nextItems, success) {
  const customer = customerId.value.trim(); if (!customer) return;
  cartLoading.value = true;
  try { const nextCart = { id: customer, userName: customer, items: nextItems }; await requestJson(BASKET_API, '/basket', { method: 'POST', body: JSON.stringify(nextCart) }); cart.value = nextCart; setNotice(success); }
  catch (err) { setError(`No se pudo actualizar el carrito: ${err.message}`); } finally { cartLoading.value = false; }
}
async function saveCustomer() {
  const customer = customerId.value.trim(); if (!customer) return setError('Ingresa tu nombre para continuar.');
  customerId.value = customer; localStorage.setItem('eshop-customer-id', customer); cart.value = { id: customer, userName: customer, items: [] }; customerModal.value = false;
  await Promise.all([loadCart(), loadOrders()]); setNotice(`Catálogo para ${customer}.`);
  if (pendingProduct.value) { const product = pendingProduct.value; pendingProduct.value = null; await addToCart(product); }
}
async function requestAdd(product) { if (!customerId.value.trim()) { pendingProduct.value = product; customerModal.value = true; return; } await addToCart(product); }
async function addToCart(product) { const found = cart.value.items.find(item => item.productId === product.id); const next = found ? cart.value.items.map(item => item.productId === product.id ? { ...item, quantity: item.quantity + 1 } : item) : [...cart.value.items, { productId: product.id, productName: product.name, quantity: 1, color: 'Predeterminado', price: product.price, addedAt: new Date().toISOString() }]; await saveCart(next, `${product.name} se agregó al carrito.`); }
async function changeQuantity(item, delta) { const next = cart.value.items.map(current => current.productId === item.productId ? { ...current, quantity: current.quantity + delta } : current).filter(current => current.quantity > 0); await saveCart(next, 'Carrito actualizado.'); }
async function removeFromCart(item) { await saveCart(cart.value.items.filter(current => current.productId !== item.productId), 'Producto eliminado del carrito.'); }
async function clearCart() { if (!cart.value.items.length || !window.confirm('¿Vaciar el carrito?')) return; cartLoading.value = true; try { await requestJson(BASKET_API, `/basket/${encodeURIComponent(customerId.value)}`, { method: 'DELETE' }); cart.value = { id: customerId.value, userName: customerId.value, items: [] }; setNotice('Carrito vaciado.'); } catch (err) { setError(`No se pudo vaciar el carrito: ${err.message}`); } finally { cartLoading.value = false; } }

async function loadOrders() { if (!customerId.value.trim()) { orders.value = []; return; } ordersLoading.value = true; try { const data = await requestJson(ORDERS_API, `/api/orders/customer/${encodeURIComponent(customerId.value.trim())}`); orders.value = data.orders || []; } catch (err) { setError(`No se pudieron cargar las órdenes: ${err.message}`); } finally { ordersLoading.value = false; } }
async function searchOrderById() { if (!orderIdSearch.value.trim()) return setError('Escribe el identificador de la orden.'); ordersLoading.value = true; try { selectedOrder.value = await requestJson(ORDERS_API, `/api/orders/${encodeURIComponent(orderIdSearch.value.trim())}`); customerId.value = selectedOrder.value.customerId; localStorage.setItem('eshop-customer-id', customerId.value); await loadOrders(); setNotice('Orden encontrada; se cargó el historial completo del cliente.'); } catch (err) { selectedOrder.value = null; setError(`No se encontró la orden: ${err.message}`); } finally { ordersLoading.value = false; } }
async function searchOrderFromPdf(event) {
  const file = event.target.files?.[0];
  if (!file) return;
  pdfLoading.value = true;
  try {
    const pdf = await getDocument({ data: new Uint8Array(await file.arrayBuffer()) }).promise;
    let text = '';
    for (let pageNumber = 1; pageNumber <= pdf.numPages; pageNumber += 1) {
      const page = await pdf.getPage(pageNumber);
      const content = await page.getTextContent();
      text += ` ${content.items.map(item => item.str).join(' ')}`;
    }
    const match = text.match(/Orden\s*:\s*([a-f0-9]{24})/i) || text.match(/Orden\s*#\s*([a-f0-9]{24})/i);
    if (!match) throw new Error('El PDF no contiene un identificador de orden válido de eShop.');
    orderIdSearch.value = match[1];
    setNotice(`Ticket adjuntado. Presiona “Buscar orden” para consultar el historial.`);
  } catch (err) { setError(`No se pudo leer el PDF: ${err.message}`); }
  finally { pdfLoading.value = false; event.target.value = ''; }
}
const cartTax = computed(() => Number((cartTotal.value * 0.16).toFixed(2)));
const cartGrandTotal = computed(() => cartTotal.value + cartTax.value);
async function checkout() {
  if (!customerId.value.trim()) return customerModal.value = true;
  if (!cart.value.items.length) return setError('Agrega al menos un producto antes de confirmar.');
  cartLoading.value = true;
  try { const result = await requestJson(ORDERS_API, '/api/orders', { method: 'POST', body: JSON.stringify({ customerId: customerId.value, basketId: cart.value.id }) }); const created = await requestJson(ORDERS_API, `/api/orders/${result.orderId}`); await requestJson(BASKET_API, `/basket/${encodeURIComponent(customerId.value)}`, { method: 'DELETE' }); cart.value = { id: customerId.value, userName: customerId.value, items: [] }; generatedOrder.value = created; selectedOrder.value = created; checkoutStage.value = 'confirmed'; setNotice(`Orden generada exitosamente: ${result.orderId}`); }
  catch (err) { setError(`No se pudo generar la compra: ${err.message}`); } finally { cartLoading.value = false; }
}
function downloadTicket(order) {
  const pdf = new jsPDF({ unit: 'mm', format: [80, 180] }); let y = 12; const line = text => { pdf.text(String(text), 8, y); y += 6; };
  pdf.setFont('helvetica', 'bold'); pdf.setFontSize(14); pdf.text('eShop - Ticket de compra', 8, y); y += 8; pdf.setFont('helvetica', 'normal'); pdf.setFontSize(9);
  line(`Orden: ${order.id}`); line(`Cliente: ${order.customerId}`); line(`Fecha: ${new Date(order.createdAt).toLocaleString('es-MX')}`); pdf.line(8, y, 72, y); y += 6;
  order.items.forEach(item => { const lines = pdf.splitTextToSize(`${item.productName} x${item.quantity}`, 46); pdf.text(lines, 8, y); pdf.text(`$${money(item.lineTotal)}`, 72, y, { align: 'right' }); y += Math.max(6, lines.length * 4.5); });
  pdf.line(8, y, 72, y); y += 6; line(`SUBTOTAL: $${money(order.subtotal)}`); line(`IVA (16%): $${money(order.tax)}`); pdf.setFont('helvetica', 'bold'); pdf.setFontSize(12); pdf.text(`TOTAL: $${money(order.total)}`, 72, y, { align: 'right' }); y += 10; pdf.setFont('helvetica', 'normal'); pdf.setFontSize(8); pdf.text('Gracias por tu compra.', 40, y, { align: 'center' }); pdf.save(`ticket-${order.id}.pdf`);
}
function downloadGeneratedOrder() { if (!generatedOrder.value) return; downloadTicket(generatedOrder.value); customerId.value = ''; localStorage.removeItem('eshop-customer-id'); cart.value = { id: '', userName: '', items: [] }; orders.value = []; selectedOrder.value = null; generatedOrder.value = null; checkoutStage.value = 'summary'; view.value = 'catalog'; setNotice('Orden descargada. Sesión de compra finalizada.'); }
function changeView(next) { view.value = next; if (next === 'cart') loadCart(); if (next === 'orders') loadOrders(); }
function searchFromFirstPage() { search.pageNumber = 1; loadProducts(); }
function changePage(delta) { search.pageNumber += delta; loadProducts(); }
onMounted(async () => { await Promise.all([loadProducts(), loadCart(), loadOrders()]); });
</script>

<template>
  <main class="app-shell">
    <section class="toolbar"><div><p class="eyebrow">Colección urbana · eShop</p><h1>{{ visibleTitle }}</h1><p class="hero-copy">Compra prendas, administra el catálogo y consulta tus comprobantes.</p></div><button class="customer-chip" type="button" @click="customerModal = true">{{ customerId ? `Catálogo para: ${customerId}` : 'Catálogo para: indicar cliente' }}</button></section>
    <nav class="view-navigation"><button :class="{ active: view === 'catalog' }" @click="changeView('catalog')">Catálogo</button><button :class="{ active: view === 'cart' }" @click="changeView('cart')">Ir a carrito ({{ cartItemsCount }})</button><button :class="{ active: view === 'orders' }" @click="changeView('orders')">Mis órdenes</button><button :class="{ active: view === 'admin' }" @click="changeView('admin')">Administración</button></nav>
    <p v-if="message" class="notice success">{{ message }}</p><p v-if="error" class="notice error">{{ error }}</p>

    <section v-if="view === 'catalog'" class="panel results">
      <div class="panel-header"><h2>Prendas disponibles</h2><span>{{ totalCount }} resultados</span></div><form class="search-row" @submit.prevent="searchFromFirstPage"><input v-model="search.name" placeholder="Buscar producto por nombre"/><select v-model.number="search.pageSize" @change="searchFromFirstPage"><option :value="8">8</option><option :value="12">12</option><option :value="20">20</option></select><button class="secondary">Buscar</button></form>
      <div class="product-grid"><p v-if="loading" class="empty-state">Cargando productos...</p><p v-else-if="!products.length" class="empty-state">No hay productos para mostrar.</p><article v-for="product in products" v-else :key="product.id" class="product-card"><img :src="imageSource(product.imageFiles)" :alt="product.name" @error="handleImageError"/><div class="product-card-content"><span class="category">{{ product.category?.join(', ') || 'Sin categoría' }}</span><h3>{{ product.name }}</h3><p>{{ product.description || 'Sin descripción.' }}</p><strong>${{ money(product.price) }}</strong><div class="card-actions"><button class="primary" :disabled="cartLoading" @click="requestAdd(product)">Agregar</button><button class="secondary" @click="productDetail = product">Detalles</button></div></div></article></div>
      <div class="pagination"><button class="secondary" :disabled="search.pageNumber <= 1" @click="changePage(-1)">Anterior</button><span>Página {{ search.pageNumber }} de {{ totalPages }}</span><button class="secondary" :disabled="search.pageNumber >= totalPages" @click="changePage(1)">Siguiente</button></div>
    </section>

    <section v-else-if="view === 'admin'" class="admin-workspace"><form class="panel editor" @submit.prevent="submitProduct"><div class="panel-header"><h2>{{ isEditing ? 'Editar producto' : 'Nuevo producto' }}</h2><button class="secondary" type="button" @click="resetForm">Limpiar</button></div><label>Nombre<input v-model="form.name" :disabled="isEditing" required/></label><label>Descripción<textarea v-model="form.description" rows="4"/></label><label>Categorías<input v-model="form.categoryText" placeholder="Ropa, Calzado"/></label><label>URL de imagen<input v-model="form.imageFiles" type="url"/></label><div class="image-preview"><img :src="imageSource(form.imageFiles)" @error="handleImageError"/><span>Vista previa</span></div><label>Precio<input v-model.number="form.price" min="0" step="0.01" type="number" required/></label><button class="primary" :disabled="saving">{{ saving ? 'Guardando...' : isEditing ? 'Guardar cambios' : 'Insertar producto' }}</button></form><section class="panel results"><div class="panel-header"><h2>Gestión de productos</h2><span>{{ totalCount }} resultados</span></div><form class="search-row" @submit.prevent="searchFromFirstPage"><input v-model="search.name" placeholder="Buscar por nombre"/><select v-model.number="search.pageSize" @change="searchFromFirstPage"><option :value="8">8</option><option :value="12">12</option></select><button class="secondary">Buscar</button></form><div class="product-grid"><article v-for="product in products" :key="product.id" class="product-card"><img :src="imageSource(product.imageFiles)" @error="handleImageError"/><div class="product-card-content"><h3>{{ product.name }}</h3><p>${{ money(product.price) }}</p><div class="card-actions"><button class="secondary" @click="editProduct(product)">Editar</button><button class="danger" :disabled="saving" @click="deleteProduct(product.name)">Eliminar</button></div></div></article></div><div class="pagination"><button class="secondary" :disabled="search.pageNumber <= 1" @click="changePage(-1)">Anterior</button><span>Página {{ search.pageNumber }} de {{ totalPages }}</span><button class="secondary" :disabled="search.pageNumber >= totalPages" @click="changePage(1)">Siguiente</button></div></section></section>

    <section v-else-if="view === 'cart'" class="single-view"><section v-if="checkoutStage === 'summary'" class="panel cart-panel"><div class="panel-header"><h2>Resumen del carrito</h2><button class="secondary" :disabled="cartLoading || !cart.items.length" @click="clearCart">Vaciar</button></div><p class="form-help">Cliente: <strong>{{ customerId || 'Sin cliente seleccionado' }}</strong></p><p v-if="!cart.items.length" class="empty-state">Aún no agregas productos.</p><ul v-else class="cart-list"><li v-for="item in cart.items" :key="item.productId"><div><strong>{{ item.productName }}</strong><small>${{ money(item.price) }} c/u<br/>Agregado: {{ item.addedAt ? new Date(item.addedAt).toLocaleString('es-MX') : 'Sin registro previo' }}</small></div><div class="quantity"><button class="secondary" @click="changeQuantity(item,-1)">−</button><span>{{ item.quantity }}</span><button class="secondary" @click="changeQuantity(item,1)">+</button></div><button class="remove" @click="removeFromCart(item)">×</button></li></ul><div class="cart-total"><span>Total de productos</span><strong>${{ money(cartTotal) }}</strong></div><button v-if="cart.items.length" class="primary checkout" @click="checkoutStage='ticket'">Continuar compra</button></section><section v-else-if="checkoutStage === 'ticket'" class="ticket-preview"><p>eShop · Vista previa</p><h2>Ticket de compra</h2><small>Identificador temporal: {{ cart.id }}<br/>Cliente: {{ customerId }}<br/>Fecha: {{ new Date().toLocaleString('es-MX') }}</small><hr/><div v-for="item in cart.items" :key="item.productId" class="ticket-line"><span>{{ item.productName }} × {{ item.quantity }}</span><strong>${{ money(item.price * item.quantity) }}</strong></div><hr/><div class="ticket-line"><span>Subtotal</span><strong>${{ money(cartTotal) }}</strong></div><div class="ticket-line"><span>IVA (16%)</span><strong>${{ money(cartTax) }}</strong></div><div class="ticket-total">TOTAL <strong>${{ money(cartGrandTotal) }}</strong></div><div class="card-actions"><button class="secondary" :disabled="cartLoading" @click="checkoutStage='summary'">Regresar</button><button class="primary checkout" :disabled="cartLoading" @click="checkout">{{ cartLoading ? 'Confirmando orden...' : 'Confirmar compra' }}</button></div></section><section v-else class="ticket-preview success-ticket"><p>eShop · Compra confirmada</p><h2>Orden generada exitosamente</h2><p>Tu orden fue guardada en MongoDB.</p><div v-if="generatedOrder" class="ticket-line"><span>Identificador único</span><strong>{{ generatedOrder.id }}</strong></div><button class="primary checkout" @click="downloadGeneratedOrder">Descargar orden</button></section></section>

    <section v-else class="single-view"><section class="panel orders-panel"><div class="panel-header"><div><h2>Consulta de órdenes con ticket PDF</h2><p class="form-help">Busca una orden y consulta todas las compras del cliente.</p></div></div><div class="order-search"><label>Cliente<input v-model="customerId" placeholder="Nombre del cliente"/></label><label>ID de orden<input v-model="orderIdSearch" placeholder="Ej. 6a7e..."/></label><button class="secondary" @click="searchOrderById">Buscar orden</button></div><label class="pdf-upload">Subir ticket PDF<input accept="application/pdf" type="file" @change="searchOrderFromPdf"/><span>{{ pdfLoading ? 'Leyendo comprobante...' : 'Seleccionar comprobante PDF' }}</span></label><button class="secondary" :disabled="pdfLoading || !orderIdSearch" @click="searchOrderById">Buscar orden del PDF</button><article v-if="selectedOrder" class="order-card"><div class="order-heading"><div><strong>Orden #{{ selectedOrder.id }}</strong><small>Cliente: {{ selectedOrder.customerId }} · {{ new Date(selectedOrder.createdAt).toLocaleString('es-MX') }}</small></div><button class="primary" @click="downloadTicket(selectedOrder)">Descargar orden</button></div><ul><li v-for="item in selectedOrder.items" :key="item.productId"><span>{{ item.productName }} × {{ item.quantity }} · ${{ money(item.unitPrice) }}</span><strong>${{ money(item.lineTotal) }}</strong></li></ul><div class="ticket-line"><span>Subtotal</span><strong>${{ money(selectedOrder.subtotal) }}</strong></div><div class="ticket-line"><span>IVA</span><strong>${{ money(selectedOrder.tax) }}</strong></div><div class="ticket-total">TOTAL <strong>${{ money(selectedOrder.total) }}</strong></div></article><div v-if="orders.length > 1" class="order-history"><h3>Todas las órdenes de {{ customerId }}</h3><article v-for="order in orders.filter(order => order.id !== selectedOrder?.id)" :key="order.id" class="order-card"><div class="order-heading"><strong>Orden #{{ order.id }}</strong><button class="secondary" @click="selectedOrder=order">Ver detalle</button></div><small>{{ new Date(order.createdAt).toLocaleString('es-MX') }} · Total ${{ money(order.total) }}</small></article></div></section></section>

    <div v-if="customerModal" class="modal-backdrop"><form class="modal-card" @submit.prevent="saveCustomer"><h2>Ingresa tu nombre</h2><p>Necesitamos identificar tu carrito y tus órdenes.</p><input v-model="customerId" autofocus placeholder="Tu nombre"/><div class="modal-actions"><button class="secondary" type="button" @click="customerModal=false">Cancelar</button><button class="primary">Guardar y continuar</button></div></form></div>
    <div v-if="productDetail" class="modal-backdrop"><article class="modal-card product-modal"><button class="close" @click="productDetail=null">×</button><img :src="imageSource(productDetail.imageFiles)" @error="handleImageError"/><span class="category">{{ productDetail.category?.join(', ') }}</span><h2>{{ productDetail.name }}</h2><p>{{ productDetail.description }}</p><strong>${{ money(productDetail.price) }}</strong><button class="primary" @click="requestAdd(productDetail); productDetail=null">Agregar al carrito</button></article></div>
  </main>
</template>
