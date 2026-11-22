import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { createApi, tokenExpiration } from './api.mjs';
import { createOrderFlow } from './operations.mjs';
import { orderBody, reconcileCart } from './cart.mjs';
import { Auth } from './Auth.jsx';
import { Catalog } from './Catalog.jsx';
import { Cart } from './Cart.jsx';
import { Orders } from './Orders.jsx';
import { Inventory } from './Inventory.jsx';
import { Button, Icon, Notice } from './ui.jsx';

export default function App() {
  const [session, setSession] = useState(null), [authOpen, setAuthOpen] = useState(false), [page, setPage] = useState('catalogo');
  const [message, setMessage] = useState('');
  const current = useRef(session); current.current = session;
  const logout = useCallback(() => { setSession(null); setPage('catalogo'); setMessage('Sesión cerrada.'); }, []);
  const request = useMemo(() => createApi({ token: session?.token || '', onExpired: () => {
    if (current.current?.token === session?.token) {
      setSession(null); setPage('catalogo'); setMessage('Tu sesión terminó. Ingresa nuevamente para continuar. Las solicitudes pendientes se conservan.'); setAuthOpen(true);
    }
  } }), [session]);
  useEffect(() => {
    if (!session) return;
    const expiry = tokenExpiration(session.token);
    const timer = setTimeout(() => {
      setSession(null); setPage('catalogo'); setMessage('Tu sesión expiró. Vuelve a ingresar para continuar.'); setAuthOpen(true);
    }, Math.max(0, Math.min(expiry - Date.now(), 2147483647)));
    return () => clearTimeout(timer);
  }, [session]);
  function login(data) { setSession(data); setAuthOpen(false); setMessage(''); setPage('catalogo'); }
  function navigate(p) {
    if (p === 'pedidos' && !session) { setAuthOpen(true); return; }
    setPage(p); setMessage('');
  }
  return <div className="app">
    <a className="skip-link" href="#contenido">Ir al contenido</a>
    <header className="site-header"><div className="header-inner"><button className="brand" onClick={() => navigate('catalogo')} aria-label="MasterMusic, inicio"><span className="brand-mark"><Icon size={24} /></span><span>Master<span>Music</span><small>INSTRUMENTOS & PEDIDOS</small></span></button>
      <nav aria-label="Navegación principal"><button className={page === 'catalogo' ? 'active' : ''} onClick={() => navigate('catalogo')}>Catálogo</button><button className={page === 'pedidos' ? 'active' : ''} onClick={() => navigate('pedidos')}>{session?.usuario.rol === 'Administrador' ? 'Pedidos' : 'Mis pedidos'}</button>{session?.usuario.rol === 'Administrador' && <button className={page === 'inventario' ? 'active' : ''} onClick={() => navigate('inventario')}>Inventario</button>}</nav>
      {session ? <div className="account"><span><strong>{session.usuario.nombre}</strong><small>{session.usuario.rol}</small></span><button className="logout" onClick={logout}>Salir</button></div> : <Button variant="header-button" icon="user" onClick={() => setAuthOpen(true)}>Ingresar</Button>}
    </div></header>
    <main id="contenido" tabIndex="-1"><Notice tone="info">{message}</Notice>
      <Workspace key={session?.usuario.idUser ?? 'public'} session={session} request={request} page={page} setPage={setPage} onLogin={() => setAuthOpen(true)} />
    </main>
    <footer><span className="footer-brand">MasterMusic<span> / </span>Tu música empieza aquí.</span><span>Precios en pesos chilenos · CLP</span></footer>
    {authOpen && <Auth request={request} onLogin={login} onClose={() => setAuthOpen(false)} />}
  </div>;
}

function Workspace({ session, request, page, setPage, onLogin }) {
  const admin = session?.usuario.rol === 'Administrador';
  const [catalog, setCatalog] = useState([]), [loading, setLoading] = useState(true), [catalogError, setCatalogError] = useState('');
  const [version, setVersion] = useState(0), [cart, setCart] = useState([]), [cartOpen, setCartOpen] = useState(false);
  const [busy, setBusy] = useState(false), [pending, setPending] = useState(null), [storageError, setStorageError] = useState('');
  const [notice, setNotice] = useState(null);
  const mounted = useRef(true), lock = useRef(false);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  const flow = useMemo(() => session ? createOrderFlow({ request, userId: session.usuario.idUser,
    storage: { getItem: k => localStorage.getItem(k), setItem: (k, v) => localStorage.setItem(k, v), removeItem: k => localStorage.removeItem(k) } }) : null, [session, request]);
  const checkPending = useCallback(() => {
    try { setPending(flow?.load() || null); setStorageError(''); } catch (e) { setStorageError(e.message); }
  }, [flow]);
  useEffect(() => { checkPending(); window.addEventListener('storage', checkPending); return () => window.removeEventListener('storage', checkPending); }, [checkPending]);
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setCatalogError('');
    request('catalogo', '/api/v1.0/Instrumentos', { signal: controller.signal, publicRequest: true })
      .then(r => setCatalog(r.data)).catch(e => { if (!controller.signal.aborted) setCatalogError(e.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [request, version]);
  const blocked = !!pending || !!storageError;
  function add(item) {
    if (!session) { onLogin(); return; }
    if (admin || blocked || busy) return;
    const existing = cart.find(l => l.idInstrumento === item.idInstrumento);
    const amount = existing ? Number(existing.cantidad) + 1 : 1;
    if (amount > item.stock) { setNotice({ tone: 'warning', text: 'Ya seleccionaste todas las unidades disponibles de este instrumento.' }); return; }
    setCart(lines => [...lines.filter(l => l.idInstrumento !== item.idInstrumento),
      { idInstrumento: item.idInstrumento, nombre: item.nombre, cantidad: amount, precioEsperado: item.precio, stock: item.stock, disponible: true }]);
    setNotice({ tone: 'success', text: item.nombre + ' agregado a tu selección.' });
  }
  async function refreshCart() {
    if (lock.current) return;
    lock.current = true; setBusy(true);
    try {
      const { data } = await request('catalogo', '/api/v1.0/Instrumentos', { publicRequest: true });
      setCatalog(data); setCart(lines => reconcileCart(lines, data));
    } catch (e) { setNotice({ tone: 'error', text: e.message }); }
    finally { lock.current = false; setBusy(false); }
  }
  async function operation(type, options) {
    if (!flow || lock.current) return false;
    lock.current = true; setBusy(true); setNotice(null);
    try {
      const result = type === 'Recuperar' ? await flow.recover() : await flow.begin(type, options);
      if (!mounted.current) return false;
      if (result.kind === 'complete') {
        if (result.type === 'Crear') setCart([]);
        setCartOpen(false); setPage('pedidos'); setVersion(v => v + 1);
        setNotice({ tone: 'success', text: 'Pedido ' + result.order.idPedido + ': ' + result.order.estado.toLowerCase() + '.' });
      } else if (result.kind === 'pending') {
        setCartOpen(false); setNotice({ tone: 'warning', text: result.message });
      } else {
        setCartOpen(false); setNotice({ tone: 'error', text: result.message + (result.type === 'Crear' ? ' Revisa tu selección y actualiza los precios y el stock antes de enviar otra vez.' : '') });
        setVersion(v => v + 1);
      }
      return true;
    } catch (e) {
      if (mounted.current) setNotice({ tone: 'error', text: e.message });
      return false;
    } finally {
      lock.current = false;
      if (mounted.current) { setBusy(false); checkPending(); }
    }
  }
  function checkout() {
    try { operation('Crear', { body: orderBody(cart) }); } catch (e) { setNotice({ tone: 'error', text: e.message }); }
  }
  return <>
    <Notice tone={notice?.tone}>{notice?.text}</Notice><Notice>{storageError}</Notice>
    {pending && <div className="pending-panel"><span className="pending-icon"><Icon name="refresh" /></span><div><strong>Hay una solicitud por verificar</strong><p>{pending.type === 'Crear' ? 'Creación de pedido' : pending.type + ' pedido'} · {new Date(pending.createdAt).toLocaleString('es-CL')}</p><small>No vuelvas a enviarla con una clave nueva.</small></div><Button variant="secondary" disabled={busy} onClick={() => operation('Recuperar')}>{busy ? 'Consultando…' : 'Consultar resultado'}</Button></div>}
    {page === 'catalogo' && <Catalog items={catalog} loading={loading} error={catalogError} onReload={() => setVersion(v => v + 1)} onAdd={add} role={session?.usuario.rol} blocked={blocked || busy} />}
    {page === 'pedidos' && session && <Orders request={request} admin={admin} version={version} blocked={blocked} busy={busy} onChange={operation} />}
    {page === 'inventario' && admin && <Inventory request={request} userId={session.usuario.idUser} onChanged={() => setVersion(v => v + 1)} />}
    {session && !admin && cart.length > 0 && <button className="cart-float" onClick={() => setCartOpen(true)}><Icon name="bag" /><span>Mi selección <strong>{cart.length}</strong></span></button>}
    {cartOpen && <Cart lines={cart} onClose={() => setCartOpen(false)} busy={busy} blocked={blocked}
      onQuantity={(id, cantidad) => setCart(lines => lines.map(l => l.idInstrumento === id ? { ...l, cantidad } : l))}
      onRemove={id => setCart(lines => lines.filter(l => l.idInstrumento !== id))}
      onSubmit={checkout} onRefresh={refreshCart} />}
  </>;
}
