import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ApiError, createApi } from '../src/api.mjs';
import { createOrderFlow } from '../src/operations.mjs';
import { orderBody, totalCart, reconcileCart } from '../src/cart.mjs';

const key = '81272fc3-5798-4395-a34f-07b4b821cc82';
const order = { idPedido: '33333333-3333-4333-8333-333333333333', detalles: [], estado: 'Pendiente', total: 1000 };
function storage() { const map = new Map(); return { getItem: k => map.get(k) ?? null, setItem: (k, v) => map.set(k, v), removeItem: k => map.delete(k) }; }
const body = { lineas: [{ idInstrumento: 1, cantidad: 2, precioEsperado: 1000 }] };

test('una respuesta perdida conserva la clave y recupera el único pedido', async () => {
  const saved = storage(); const calls = [];
  const flow = createOrderFlow({ storage: saved, userId: 7, uuid: () => key, request: async (service, path, options) => {
    calls.push({ path, options });
    if (path.endsWith('/Pedidos')) throw new ApiError(0);
    return { data: { estadoOperacion: 'Completada', pedido: order, recuperable: false } };
  } });
  assert.equal((await flow.begin('Crear', { body })).kind, 'pending');
  assert.equal(flow.load().key, key);
  assert.equal((await flow.recover()).order.idPedido, order.idPedido);
  assert.equal(flow.load(), null);
  assert.equal(calls.filter(c => c.options?.method === 'POST').length, 1);
});

test('si la operación no existe reenvía exactamente el cuerpo y la clave guardados', async () => {
  const saved = storage(); const posts = [];
  const flow = createOrderFlow({ storage: saved, userId: 7, uuid: () => key, request: async (service, path, options) => {
    if (path.includes('/Operaciones?')) throw new ApiError(404);
    posts.push(options);
    if (posts.length === 1) throw new ApiError(503);
    return { data: order };
  } });
  await flow.begin('Crear', { body });
  await flow.recover();
  assert.deepEqual(posts[0], posts[1]);
  assert.equal(posts[1].key, key);
});
test('un doble envío en curso no produce dos solicitudes', async () => {
  let release; let posts = 0;
  const response = new Promise(r => { release = r; });
  const flow = createOrderFlow({ storage: storage(), userId: 7, uuid: () => key, request: async () => { posts++; return response; } });
  const first = flow.begin('Crear', { body });
  await assert.rejects(flow.begin('Crear', { body }), /curso/);
  release({ data: order }); await first;
  assert.equal(posts, 1);
});
test('una operación pendiente bloquea un nuevo pedido incluso después de recargar', async () => {
  const saved = storage();
  const flow = createOrderFlow({ storage: saved, userId: 7, uuid: () => key, request: async () => { throw new ApiError(0); } });
  await flow.begin('Crear', { body });
  const restored = createOrderFlow({ storage: saved, userId: 7, request: async () => { throw Error('No debe enviarse'); } });
  await assert.rejects(restored.begin('Crear', { body }), /pendiente/);
  assert.equal(restored.load().key, key);
});
test('un rechazo de negocio libera la solicitud para corregir la selección', async () => {
  const flow = createOrderFlow({ storage: storage(), userId: 7, uuid: () => key, request: async () => { throw new ApiError(409, { codigo: 'STOCK_INSUFICIENTE', detail: 'Stock insuficiente' }); } });
  const result = await flow.begin('Crear', { body });
  assert.equal(result.kind, 'rejected'); assert.equal(flow.load(), null);
});
test('un 401 conserva la solicitud pendiente para continuar después del acceso', async () => {
  const flow = createOrderFlow({ storage: storage(), userId: 7, uuid: () => key, request: async () => { throw new ApiError(401); } });
  await assert.rejects(flow.begin('Crear', { body }), e => e.status === 401);
  assert.equal(flow.load().key, key);
});
test('cada cuenta solo carga su solicitud guardada', async () => {
  const saved = storage();
  const a = createOrderFlow({ storage: saved, userId: 7, uuid: () => key, request: async () => { throw new ApiError(0); } });
  await a.begin('Crear', { body });
  const b = createOrderFlow({ storage: saved, userId: 8, request: async () => {} });
  assert.equal(b.load(), null);
});
test('un fallo de almacenamiento impide enviar un pedido', async () => {
  let called = false;
  const flow = createOrderFlow({ storage: { getItem: () => null, setItem: () => { throw Error('quota'); } }, userId: 7, uuid: () => key,
    request: async () => { called = true; } });
  await assert.rejects(flow.begin('Crear', { body }), /almacenamiento/); assert.equal(called, false);
});
test('recupera una cancelación usando el identificador de operación del servidor', async () => {
  const calls = [];
  const op = { idOperacion: key, recuperable: true, estadoOperacion: 'EnProceso' };
  const flow = createOrderFlow({ storage: storage(), userId: 7, uuid: () => key, request: async (service, path, options) => {
    calls.push(path);
    if (path.endsWith('/recuperacion')) return { data: { ...op, recuperable: false, estadoOperacion: 'Completada', pedido: { ...order, estado: 'Cancelado' } } };
    return { status: 202, data: op };
  } });
  await flow.begin('Cancelar', { orderId: order.idPedido, body: { motivo: 'Pedido cancelado' } });
  assert.equal((await flow.recover()).order.estado, 'Cancelado');
  assert.equal(calls.filter(p => p.endsWith('/cancelacion')).length, 1);
  assert.equal(calls.at(-1), '/api/v1.0/Operaciones/' + key + '/recuperacion');
});
test('un segundo timeout mantiene la operación recuperable', async () => {
  const flow = createOrderFlow({ storage: storage(), userId: 7, uuid: () => key, request: async () => { throw new ApiError(0); } });
  await flow.begin('Crear', { body });
  assert.equal((await flow.recover()).kind, 'pending'); assert.equal(flow.load().key, key);
});
test('un 401 protegido cierra la sesión; un 403 no la elimina', async () => {
  let expired = 0, status = 403;
  const request = createApi({ token: 'TOKEN-DE-PRUEBA', onExpired: () => expired++, fetcher: async () => new Response('{}', { status }) });
  await assert.rejects(request('pedidos', '/api/v1.0/Pedidos'), e => e.status === 403);
  assert.equal(expired, 0); status = 401;
  await assert.rejects(request('pedidos', '/api/v1.0/Pedidos'), e => e.status === 401);
  assert.equal(expired, 1);
});
test('los datos de acceso se envían al catálogo y el JWT de memoria a pedidos', async () => {
  const calls = [];
  const request = createApi({ token: 'TOKEN-DE-PRUEBA', catalogo: 'https://catalogo.test', pedidos: 'https://pedidos.test',
    fetcher: async (url, options) => { calls.push({ url, options }); return new Response('{}'); } });
  await request('catalogo', '/api/v1.0/Auth/login', { method: 'POST', publicRequest: true, body: { correo: 'u@example.com', clave: 'solo-prueba' } });
  await request('pedidos', '/api/v1.0/Pedidos');
  assert.equal(calls[0].options.headers.Authorization, undefined);
  assert.equal(calls[1].url, 'https://pedidos.test/api/v1.0/Pedidos');
  assert.equal(calls[1].options.headers.Authorization, 'Bearer TOKEN-DE-PRUEBA');
});
test('una respuesta inválida tras una escritura se considera incierta', async () => {
  const request = createApi({ fetcher: async () => new Response('<html>Error</html>', { status: 200 }) });
  await assert.rejects(request('pedidos', '/api/v1.0/Pedidos', { method: 'POST' }), e => e.uncertain);
});
test('la selección solo envía IDs, cantidades y precios esperados', () => {
  assert.deepEqual(orderBody([{ ...body.lineas[0], nombre: 'Guitarra', stock: 10 }]), body);
});
test('rechaza cantidades fraccionarias y productos repetidos', () => {
  assert.throws(() => orderBody([{ ...body.lineas[0], cantidad: 1.5 }]));
  assert.throws(() => orderBody([body.lineas[0], body.lineas[0]]), /repetidos/);
});
test('evita totales fuera del rango exacto de JavaScript', () => {
  assert.throws(() => totalCart([{ cantidad: 2147483647, precioEsperado: 2147483647 }]), /máximo/);
});
test('actualizar el catálogo marca lo retirado y actualiza el precio para una nueva revisión', () => {
  const result = reconcileCart([{ idInstrumento: 1, cantidad: 2, precioEsperado: 1000 }, { idInstrumento: 2, cantidad: 1, precioEsperado: 500 }],
    [{ idInstrumento: 1, activo: true, stock: 1, precio: 1200, nombre: 'Guitarra' }]);
  assert.equal(result[0].precioEsperado, 1200); assert.equal(result[0].stock, 1); assert.equal(result[1].disponible, false);
});
