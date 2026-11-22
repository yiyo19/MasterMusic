import React, { useEffect, useState } from 'react';
import { money, date } from './cart.mjs';
import { Button, Empty, Loading, Modal, Notice, Status } from './ui.jsx';
export function Orders({ request, admin, version, blocked, busy, onChange }) {
  const [items, setItems] = useState([]), [loading, setLoading] = useState(true), [error, setError] = useState('');
  const [reload, setReload] = useState(0), [filter, setFilter] = useState('Todos'), [dialog, setDialog] = useState(null);
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError('');
    request('pedidos', '/api/v1.0/' + (admin ? 'admin/' : '') + 'Pedidos', { signal: controller.signal })
      .then(r => setItems(r.data)).catch(e => { if (!controller.signal.aborted) setError(e.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [request, admin, version, reload]);
  const visible = items.filter(p => filter === 'Todos' || p.estado === filter);
  async function act(e) {
    e.preventDefault();
    const form = new FormData(e.currentTarget);
    const success = await onChange(dialog.type, { orderId: dialog.order.idPedido,
      body: dialog.type === 'Cancelar' ? { motivo: String(form.get('motivo')).trim() } : undefined });
    if (success) setDialog(null);
  }
  return <section className="section">
    <div className="section-title"><div><p className="eyebrow">{admin ? 'Administración' : 'Tu cuenta'}</p><h1>{admin ? 'Gestión de pedidos' : 'Mis pedidos'}</h1><p className="muted">{admin ? 'Confirma o cancela los pedidos pendientes.' : 'Consulta el estado y el detalle de tus compras.'}</p></div><Button variant="secondary" icon="refresh" disabled={loading || busy} onClick={() => setReload(x => x + 1)}>Actualizar</Button></div>
    <div className="tabs" role="group" aria-label="Filtrar pedidos">{['Todos', 'Pendiente', 'Confirmado', 'Cancelado'].map(s => <button key={s} className={filter === s ? 'active' : ''} onClick={() => setFilter(s)}>{s === 'Todos' ? 'Todos' : s + 's'} <span>{items.filter(i => s === 'Todos' || i.estado === s).length}</span></button>)}</div>
    <Notice>{error}</Notice>
    {loading ? <Loading>Cargando pedidos…</Loading> : error ? null : !visible.length ? <Empty title="No hay pedidos en esta vista" icon="bag">{admin ? 'Los pedidos aparecerán cuando un cliente confirme su selección.' : 'Cuando envíes tu selección, podrás seguirla desde aquí.'}</Empty> :
      <div className="orders-list">{visible.map(p => <article className="order-card" key={p.idPedido}>
        <div className="order-top"><div><p className="small-label">Pedido</p><code title={p.idPedido}>#{p.idPedido}</code><p className="muted small">{date(p.fecha)}{admin && ' · Cliente ' + p.idCliente}</p></div><Status value={p.estado} /></div>
        <details><summary>Ver {p.detalles.length} {p.detalles.length === 1 ? 'instrumento' : 'instrumentos'}</summary><ul className="order-lines">{p.detalles.map(d => <li key={d.idInstrumento}><span><strong>{d.nombreInstrumento}</strong><small>{d.cantidad} × {money(d.precioUnitario)}</small></span><strong>{money(d.cantidad * d.precioUnitario)}</strong></li>)}</ul></details>
        {p.motivoCancelacion && <div className="cancel-reason"><strong>Motivo de cancelación</strong><p>{p.motivoCancelacion}</p></div>}
        <div className="order-bottom"><span>Total <strong>{money(p.total)}</strong><small> CLP</small></span>{admin && p.estado === 'Pendiente' && <div className="actions"><Button variant="ghost-danger" disabled={blocked || busy} onClick={() => setDialog({ type: 'Cancelar', order: p })}>Cancelar pedido</Button><Button icon="check" disabled={blocked || busy} onClick={() => setDialog({ type: 'Confirmar', order: p })}>Confirmar</Button></div>}</div>
      </article>)}</div>}
    {dialog && <Modal title={dialog.type === 'Cancelar' ? 'Cancelar pedido' : 'Confirmar pedido'} onClose={() => setDialog(null)} busy={busy}>
      <p>Pedido <code>{dialog.order.idPedido}</code></p><p>{dialog.type === 'Cancelar' ? 'Se devolverán al inventario las cantidades descontadas. Esta acción es definitiva.' : 'El pedido quedará confirmado. Esta acción es definitiva.'}</p>
      <form onSubmit={act}><fieldset disabled={busy || blocked}>
        {dialog.type === 'Cancelar' && <label>Motivo de cancelación<textarea name="motivo" required maxLength={500} rows={4} /></label>}
        <div className="actions end"><Button type="button" variant="secondary" onClick={() => setDialog(null)}>Volver</Button><Button type="submit" variant={dialog.type === 'Cancelar' ? 'danger' : 'primary'}>{busy ? 'Procesando…' : dialog.type === 'Cancelar' ? 'Cancelar y devolver stock' : 'Confirmar pedido'}</Button></div>
      </fieldset></form>
    </Modal>}
  </section>;
}
