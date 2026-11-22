import React, { useState } from 'react';
import { Button, Empty, Icon, Loading, Modal, Notice, Photo, Status } from './ui.jsx';
import { money } from './cart.mjs';

export function Catalog({ items, loading, error, onReload, onAdd, role, blocked }) {
  const [query, setQuery] = useState(''), [category, setCategory] = useState('Todas'), [detail, setDetail] = useState(null);
  const categories = [...new Set(items.map(i => i.nombreCategoria).filter(Boolean))].sort();
  const normalize = s => s.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
  const visible = items.filter(i => (category === 'Todas' || i.nombreCategoria === category) &&
    normalize(i.nombre + ' ' + i.nombreMarca + ' ' + (i.modelo || '')).includes(normalize(query)));
  return <section>
    <div className="catalog-hero"><div><p className="eyebrow">Instrumentos para tu próximo proyecto</p><h1>Encuentra<br />tu próximo sonido<span>.</span></h1><p>Explora el catálogo, elige tus instrumentos<br className="desktop-break" /> y sigue cada pedido en un solo lugar.</p></div>
      <div className="hero-art" aria-hidden="true"><div className="orbit" /><div className="soundbars">{[24, 40, 68, 100, 78, 48, 88, 120, 84, 42, 62, 30].map((h, i) => <span key={i} style={{ height: h }} />)}</div><span className="hero-stamp">MASTER MUSIC<br /><small>DALE FORMA A TU MÚSICA</small></span></div>
    </div>
    <div className="catalog-toolbar"><div><h2>Catálogo de instrumentos</h2><span className="muted">{loading ? 'Consultando disponibilidad' : items.length + ' instrumentos activos'}</span></div><Button variant="secondary" icon="refresh" disabled={loading} onClick={onReload}>Actualizar</Button></div>
    <div className="filter-bar"><label className="search"><Icon name="search" /><span className="sr-only">Buscar instrumento, modelo o marca</span><input type="search" value={query} onChange={e => setQuery(e.target.value)} placeholder="Busca por instrumento, modelo o marca" /></label><label className="category-filter"><span className="sr-only">Categoría</span><select value={category} onChange={e => setCategory(e.target.value)}><option value="Todas">Todas las categorías</option>{categories.map(c => <option key={c}>{c}</option>)}</select></label></div>
    <Notice>{error}</Notice>
    {loading ? <Loading>Cargando instrumentos…</Loading> : error ? null : !visible.length ?
      <Empty title={items.length ? 'No encontramos coincidencias' : 'El catálogo está esperando su primer instrumento'} action={items.length ? <Button variant="secondary" onClick={() => { setQuery(''); setCategory('Todas'); }}>Limpiar filtros</Button> : null}>{items.length ? 'Prueba con otra búsqueda o categoría.' : 'Los instrumentos activos del inventario aparecerán aquí.'}</Empty> :
      <div className="product-grid">{visible.map(i => <article className="product-card" key={i.idInstrumento}>
        <button className="photo-button" onClick={() => setDetail(i)} aria-label={'Ver detalle de ' + i.nombre}><Photo item={i} /></button>
        <div className="product-body"><p className="eyebrow">{i.nombreMarca || 'Instrumento'} · {i.nombreCategoria || 'Catálogo'}</p><button className="product-name" onClick={() => setDetail(i)}>{i.nombre}</button><p className="product-model">{i.modelo || 'Modelo no informado'}</p>
          <div className="product-price">{money(i.precio)} <small>CLP</small></div><div className="product-foot"><span className={'stock ' + (!i.stock ? 'out' : '')}><span />{i.stock ? i.stock + ' disponibles' : 'Agotado'}</span>
            {role !== 'Administrador' && <button className="add-button" disabled={!i.stock || blocked} onClick={() => onAdd(i)} aria-label={'Agregar ' + i.nombre}><Icon name="plus" /></button>}
          </div></div>
      </article>)}</div>}
    {detail && <Modal title={detail.nombre} onClose={() => setDetail(null)} wide><div className="product-detail"><Photo item={detail} large /><div><p className="eyebrow">{detail.nombreMarca} · {detail.nombreCategoria}</p><h3>{detail.modelo || 'Modelo no informado'}</h3><p className="detail-description">{detail.descripcion || 'Descripción no informada.'}</p><p className="product-price">{money(detail.precio)} <small>CLP</small></p><Status value={detail.stock ? 'Disponible' : 'Agotado'} /><p className="muted">{detail.stock} unidades en stock</p>
      {role !== 'Administrador' && <Button icon="plus" disabled={!detail.stock || blocked} onClick={() => { onAdd(detail); setDetail(null); }}>Agregar a mi selección</Button>}</div></div></Modal>}
  </section>;
}
