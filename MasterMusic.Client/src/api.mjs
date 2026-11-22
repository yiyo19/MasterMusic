const configuracion = import.meta.env ?? {};
export class ApiError extends Error {
  constructor(status, data = {}) {
    const fields = Object.values(data.errors ?? {}).flat().filter(Boolean).join(' ');
    super(fields || data.detail || data.mensaje || data.message || data.title ||
      (status === 0 ? 'No pudimos conectar con el servicio. Comprueba que las dos APIs estén encendidas.' : 'No se pudo completar la solicitud.'));
    this.name = 'ApiError'; this.status = status; this.data = data;
    this.uncertain = status === 0 || status >= 500;
  }
}
export function createApi({ token = '', onExpired = () => {}, fetcher = globalThis.fetch,
  catalogo = configuracion.VITE_CATALOGO_URL || 'https://localhost:7052',
  pedidos = configuracion.VITE_PEDIDOS_URL || 'https://localhost:7267' } = {}) {
  return async function request(service, path, { method = 'GET', body, key, signal, publicRequest = false } = {}) {
    const base = service === 'catalogo' ? catalogo : pedidos;
    const headers = { Accept: 'application/json' };
    if (!publicRequest && token) headers.Authorization = 'Bearer ' + token;
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    if (key) headers['Idempotency-Key'] = key;
    const timeout = AbortSignal.timeout(20000);
    let response;
    try {
      response = await fetcher(base.replace(/\/$/, '') + path, {
        method, headers, body: body === undefined ? undefined : JSON.stringify(body),
        signal: signal ? AbortSignal.any([signal, timeout]) : timeout,
        cache: 'no-store', redirect: 'error', credentials: 'omit'
      });
    } catch (error) {
      if (signal?.aborted) throw error;
      throw new ApiError(0);
    }
    if (response.status === 401 && token && !publicRequest) onExpired();
    let data = null;
    try {
      const text = await response.text();
      if (text) data = JSON.parse(text);
    } catch { throw new ApiError(response.ok ? 0 : response.status); }
    if (!response.ok) {
      throw new ApiError(response.status, data || {});
    }
    return { status: response.status, data, location: response.headers.get('Location') };
  };
}
export function tokenExpiration(token) {
  try { return JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))).exp * 1000; }
  catch { return 0; }
}
