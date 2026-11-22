import React, { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from './api.mjs';
import { money, MAX_INT } from './cart.mjs';
import { Button, Empty, Icon, Loading, Modal, Notice, Status } from './ui.jsx';

export function Inventory({ request, userId, onChanged }) {
  const [tab, setTab] = useState('Instrumentos'), [data, setData] = useState({ Instrumentos: [], Categorias: [], Marcas: [] });
  const [loading, setLoading] = useState(true), [busy, setBusy] = useState(false), [error, setError] = useState('');
  const [formError, setFormError] = useState(''), [dialog, setDialog] = useState(null), [search, setSearch] = useState('');
  const warningKey = 'mastermusic:catalogo-por-verificar:' + userId;
  const [uncertain, setUncertain] = useState(() => { try { return localStorage.getItem(warningKey); } catch { return null; } });
  const [inspected, setInspected] = useState(false);
  const lock = useRef(false);
  const reload = useCallback(async () => {
    setLoading(true); setError('');
    try {
      const responses = await Promise.all(['admin/Instrumentos', 'Categorias', 'Marcas'].map(p => request('catalogo', '/api/v1.0/' + p)));
      setData({ Instrumentos: responses[0].data, Categorias: responses[1].data, Marcas: responses[2].data });
      setInspected(true);
    } catch (e) { setError(e.message); } finally { setLoading(false); }
  }, [request]);
  useEffect(() => { reload(); }, [reload]);
  function open(kind, record, resource = tab) { setFormError(''); setDialog({ kind, record, resource }); }
  function acknowledge() {
    try { localStorage.removeItem(warningKey); setUncertain(null); } catch { setError('No se pudo limpiar el aviso guardado.'); }
  }
  async function submit(e) {
    e.preventDefault(); if (lock.current || uncertain) return;
    const f = new FormData(e.currentTarget), d = dialog, r = d.record;
    const idKey = { Instrumentos: 'idInstrumento', Categorias: 'idCategoria', Marcas: 'idMarca' }[d.resource];
    let path = '/api/v1.0/' + d.resource + (r ? '/' + r[idKey] : ''), method = r ? 'PUT' : 'POST', body;
    try {
      const str = name => String(f.get(name) || '').trim();
      const number = (name, min) => { const n = Number(f.get(name)); if (!Number.isInteger(n) || n < min || n > MAX_INT) throw Error('Revisa los valores numéricos. Deben ser enteros dentro del rango permitido.'); return n; };
      if (d.kind === 'delete') method = 'DELETE';
      else if (d.kind === 'state') { method = 'PATCH'; path += '/estado'; body = { activo: !r.activo }; }
      else if (d.kind === 'stock') { method = 'POST'; path += '/existencias'; body = { cantidad: number('cantidad', 1) }; }
      else if (d.resource === 'Instrumentos') {
        body = { nombre: str('nombre'), modelo: str('modelo') || null, precio: number('precio', 1),
          descripcion: str('descripcion') || null, foto: str('foto') || null,
          idCategoria: number('idCategoria', 1), idMarca: number('idMarca', 1) };
        if (r) body.idInstrumento = r.idInstrumento; else body.stock = number('stock', 0);
      } else if (d.resource === 'Categorias') body = { nombre: str('nombre'), descripcion: str('descripcion') || null };
      else body = { nombre: str('nombre'), pais: str('pais') || null };
      lock.current = true; setBusy(true); setFormError('');
      // Conserva un aviso antes de enviar una escritura que no admite reintentos automáticos.
      localStorage.setItem(warningKey, 'Revisa el resultado de la última modificación de ' + d.resource.toLowerCase() + ' antes de repetirla.');
      await request('catalogo', path, { method, body });
      localStorage.removeItem(warningKey);
      setDialog(null); await reload(); onChanged();
    } catch (e) {
      if (e instanceof ApiError && e.uncertain) {
        setUncertain(localStorage.getItem(warningKey) || 'La respuesta no llegó. Revisa el resultado antes de repetir.');
        setInspected(false); setDialog(null);
      } else {
        try { localStorage.removeItem(warningKey); } catch { /* No contiene credenciales. */ }
        setFormError(e.message);
      }
    } finally { lock.current = false; setBusy(false); }
  }
  const list = data[tab].filter(i => i.nombre.toLowerCase().includes(search.toLowerCase()));
  const idKey = { Instrumentos: 'idInstrumento', Categorias: 'idCategoria', Marcas: 'idMarca' }[tab];
  const r = dialog?.record;
  const disabled = busy || loading || !!uncertain;
  const titles = { stock: 'Registrar entrada de stock', state: r?.activo ? 'Desactivar instrumento' : 'Activar instrumento', delete: 'Eliminar registro', edit: r ? 'Editar registro' : 'Agregar registro' };
  return <section className="section">
    <div className="section-title"><div><p className="eyebrow">Administración</p><h1>Inventario y catálogo</h1><p className="muted">Gestiona instrumentos, categorías y marcas.</p></div><Button variant="secondary" icon="refresh" disabled={loading || busy} onClick={reload}>Actualizar</Button></div>
    {uncertain && <Notice tone="warning"><p>{uncertain}</p><p>Actualiza la lista y comprueba los datos. No se repetirá la modificación automáticamente.</p><Button variant="secondary" disabled={!inspected || loading} onClick={acknowledge}>Ya revisé el resultado; continuar</Button></Notice>}
    <div className="tabs" role="group" aria-label="Tipo de inventario">{Object.keys(data).map(t => <button key={t} className={tab === t ? 'active' : ''} onClick={() => { setTab(t); setSearch(''); }}>{t} <span>{data[t].length}</span></button>)}</div>
    <div className="filter-bar"><label className="search"><Icon name="search" /><span className="sr-only">Buscar por nombre</span><input type="search" placeholder="Buscar por nombre" value={search} onChange={e => setSearch(e.target.value)} /></label><Button icon="plus" disabled={disabled} onClick={() => open('edit')}>Agregar {tab === 'Instrumentos' ? 'instrumento' : tab === 'Categorias' ? 'categoría' : 'marca'}</Button></div>
    <Notice>{error}</Notice>
    {loading ? <Loading>Cargando inventario…</Loading> : error ? null : !list.length ? <Empty title="No hay registros en esta vista">Usa el botón Agregar para crear uno, o cambia la búsqueda.</Empty> :
      <div className="table-scroll"><table><caption className="sr-only">{tab}</caption><thead><tr><th>Nombre</th>{tab === 'Instrumentos' ? <><th>Precio</th><th>Stock</th><th>Estado</th></> : <th>{tab === 'Marcas' ? 'País' : 'Descripción'}</th>}<th>Acciones</th></tr></thead><tbody>{list.map(i => <tr key={i[idKey]}>
        <td><strong>{i.nombre}</strong>{tab === 'Instrumentos' && <small>{i.nombreMarca} · {i.nombreCategoria}</small>}</td>
        {tab === 'Instrumentos' ? <><td className="nowrap">{money(i.precio)}</td><td>{i.stock}</td><td><Status value={i.activo ? 'Activo' : 'Inactivo'} /></td></> : <td>{(tab === 'Marcas' ? i.pais : i.descripcion) || 'No informado'}</td>}
        <td><div className="row-actions"><button className="text-button" disabled={disabled} onClick={() => open('edit', i)}>Editar</button>{tab === 'Instrumentos' && <><button className="text-button" disabled={disabled} onClick={() => open('stock', i)}>Sumar stock</button><button className="text-button" disabled={disabled} onClick={() => open('state', i)}>{i.activo ? 'Desactivar' : 'Activar'}</button></>}<button className="text-button danger-text" disabled={disabled} onClick={() => open('delete', i)}>Eliminar</button></div></td></tr>)}</tbody></table></div>}
    {dialog && <Modal title={titles[dialog.kind]} onClose={() => setDialog(null)} busy={busy} wide={dialog.kind === 'edit' && dialog.resource === 'Instrumentos'}>
      <form onSubmit={submit}><fieldset disabled={busy}>
        {dialog.kind === 'stock' ? <><p><strong>{r.nombre}</strong> · Stock actual: {r.stock}</p><label>Cantidad que ingresa<input name="cantidad" type="number" min="1" max={MAX_INT} step="1" required autoFocus /></label><p className="muted small">Se sumará a las existencias actuales. Una venta simultánea no será sobrescrita.</p></> :
        dialog.kind === 'state' ? <p>{r.activo ? 'El instrumento dejará de aparecer en el catálogo público. Se conservarán los pedidos anteriores.' : 'El instrumento volverá a aparecer en el catálogo público.'}<br /><strong>{r.nombre}</strong></p> :
        dialog.kind === 'delete' ? <p>Se eliminará <strong>{r.nombre}</strong> si no tiene referencias. Los registros que estén en uso se conservan.</p> :
        <div className={dialog.resource === 'Instrumentos' ? 'form-grid' : ''}>
          <label>Nombre<input name="nombre" defaultValue={r?.nombre || ''} required maxLength={dialog.resource === 'Instrumentos' ? 150 : dialog.resource === 'Categorias' ? 80 : 100} autoFocus /></label>
          {dialog.resource === 'Instrumentos' ? <>
            <label>Modelo<input name="modelo" defaultValue={r?.modelo || ''} maxLength={100} /></label>
            <label>Precio en CLP<input name="precio" type="number" min="1" max={MAX_INT} step="1" defaultValue={r?.precio || ''} required /></label>
            {!r && <label>Stock inicial<input name="stock" type="number" min="0" max={MAX_INT} step="1" defaultValue="0" required /></label>}
            <label>Categoría<select name="idCategoria" defaultValue={r?.idCategoria || ''} required><option value="" disabled>Seleccionar categoría</option>{data.Categorias.map(c => <option key={c.idCategoria} value={c.idCategoria}>{c.nombre}</option>)}</select></label>
            <label>Marca<select name="idMarca" defaultValue={r?.idMarca || ''} required><option value="" disabled>Seleccionar marca</option>{data.Marcas.map(m => <option key={m.idMarca} value={m.idMarca}>{m.nombre}</option>)}</select></label>
            <label className="span-all">Descripción<textarea name="descripcion" defaultValue={r?.descripcion || ''} rows={3} /></label>
            <label className="span-all">URL de imagen (opcional)<input name="foto" type="url" placeholder="https://…" defaultValue={r?.foto || ''} /></label>
            {(!data.Categorias.length || !data.Marcas.length) && <Notice tone="warning">Crea primero una categoría y una marca en las pestañas del inventario.</Notice>}
          </> : dialog.resource === 'Categorias' ? <label>Descripción<textarea name="descripcion" defaultValue={r?.descripcion || ''} maxLength={250} rows={3} /></label> : <label>País<input name="pais" defaultValue={r?.pais || ''} maxLength={60} /></label>}
        </div>}
        <Notice>{formError}</Notice><div className="actions end"><Button type="button" variant="secondary" onClick={() => setDialog(null)}>Volver</Button><Button variant={dialog.kind === 'delete' ? 'danger' : 'primary'} type="submit">{busy ? 'Guardando…' : dialog.kind === 'delete' ? 'Eliminar' : 'Guardar'}</Button></div>
      </fieldset></form>
    </Modal>}
  </section>;
}
