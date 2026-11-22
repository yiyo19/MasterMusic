import { ApiError } from './api.mjs';
const prefix = '/api/v1.0';
const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// Solo persiste el comando y su clave. El JWT permanece exclusivamente en memoria.
export function createOrderFlow({ request, storage, userId, uuid = () => crypto.randomUUID() }) {
  const storageKey = 'mastermusic:operacion:' + userId;
  let busy = false;
  function load() {
    const raw = storage.getItem(storageKey);
    if (!raw) return null;
    let o;
    try { o = JSON.parse(raw); } catch { throw Error('No se pudo leer la solicitud guardada. No la repitas hasta revisar tus pedidos.'); }
    if (o.userId !== userId || !uuidPattern.test(o.key) || !['Crear', 'Confirmar', 'Cancelar'].includes(o.type) ||
        (o.type !== 'Crear' && !uuidPattern.test(o.orderId || '')))
      throw Error('La solicitud guardada no es válida. Revisa tus pedidos antes de volver a enviar.');
    return o;
  }
  function save(command) {
    try {
      storage.setItem(storageKey, JSON.stringify(command));
      if (storage.getItem(storageKey) !== JSON.stringify(command)) throw Error();
    } catch { throw Error('El navegador no permite guardar la solicitud. Activa el almacenamiento del sitio antes de enviarla.'); }
  }
  function finish(result) {
    storage.removeItem(storageKey);
    return result;
  }
  function interpret(data, command) {
    if (data?.estadoOperacion === 'Completada' && data.pedido?.idPedido)
      return finish({ kind: 'complete', order: data.pedido, type: command.type });
    if (['Rechazada', 'Compensada'].includes(data?.estadoOperacion))
      return finish({ kind: 'rejected', message: data.problema?.detail || 'La operación no se completó.', problem: data.problema, type: command.type });
    if (!data?.estadoOperacion && data?.idPedido && Array.isArray(data.detalles))
      return finish({ kind: 'complete', order: data, type: command.type });
    return { kind: 'pending', message: 'Estamos verificando el resultado. Conservamos tu solicitud para consultarla sin duplicarla.' };
  }
  async function send(command) {
    const path = command.type === 'Crear' ? prefix + '/Pedidos'
      : prefix + '/admin/Pedidos/' + command.orderId + '/' + (command.type === 'Confirmar' ? 'confirmacion' : 'cancelacion');
    try {
      const { data } = await request('pedidos', path, { method: 'POST', body: command.body, key: command.key });
      return interpret(data, command);
    } catch (error) {
      if (!(error instanceof ApiError)) throw error;
      if ([401, 403].includes(error.status)) throw error;
      if (error.uncertain || error.data.codigo === 'IDEMPOTENCIA_CONFLICTO')
        return { kind: 'pending', message: error.uncertain
          ? 'No llegó una respuesta definitiva. Consulta el resultado antes de intentar otra compra.'
          : 'La clave ya tiene una operación asociada. Consulta su resultado.' };
      return finish({ kind: 'rejected', message: error.message, problem: error.data, type: command.type });
    }
  }
  async function exclusive(action) {
    if (busy) throw Error('Hay una solicitud en curso. Espera su resultado.');
    busy = true;
    try { return await action(); } finally { busy = false; }
  }
  async function begin(type, { orderId, body } = {}) {
    return exclusive(async () => {
      if (load()) throw Error('Primero consulta la solicitud que quedó pendiente.');
      const command = { userId, key: uuid(), type, orderId,
        body: body === undefined ? undefined : JSON.parse(JSON.stringify(body)), createdAt: new Date().toISOString() };
      save(command);
      return send(command);
    });
  }
  async function recover() {
    return exclusive(async () => {
      const command = load();
      if (!command) throw Error('No hay una operación pendiente.');
      let operation;
      try {
        const query = new URLSearchParams({ claveCliente: command.key, tipo: command.type });
        operation = (await request('pedidos', prefix + '/Operaciones?' + query)).data;
      } catch (error) {
        // Si el servidor nunca recibió el comando, se reenvía con la MISMA clave y cuerpo.
        if (error instanceof ApiError && error.status === 404) return send(command);
        if (error instanceof ApiError && error.uncertain) return { kind: 'pending', message: 'El servicio sigue sin responder. Tu solicitud se conserva.' };
        throw error;
      }
      if (operation?.recuperable && operation.idOperacion) {
        try { operation = (await request('pedidos', prefix + '/Operaciones/' + operation.idOperacion + '/recuperacion', { method: 'POST' })).data; }
        catch (error) {
          if (error instanceof ApiError && (error.uncertain || error.status === 409))
            return { kind: 'pending', message: 'La recuperación continúa pendiente. Puedes consultar de nuevo.' };
          throw error;
        }
      }
      return interpret(operation, command);
    });
  }
  return { load, begin, recover };
}
