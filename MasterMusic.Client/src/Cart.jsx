import React, { useEffect, useState } from 'react';
import { Button, Empty, Modal, Notice } from './ui.jsx';
import { money, totalCart, quantity } from './cart.mjs';

export function Cart({ lines, onClose, onQuantity, onRemove, onSubmit, onRefresh, busy, blocked }) {
  const [reviewed, setReviewed] = useState(false);
  useEffect(() => setReviewed(false), [lines]);
  let total = 0, invalid = '';
  try {
    total = totalCart(lines);
    if (lines.some(l => !l.disponible || quantity(l.cantidad) > l.stock))
      invalid = 'Revisa las cantidades: hay instrumentos agotados, inactivos o sin stock suficiente.';
  } catch (e) { invalid = e.message; }
  return <Modal title="Tu selección" onClose={onClose} busy={busy} wide>
    {!lines.length ? <Empty title="Todavía no has elegido instrumentos" icon="bag">Explora el catálogo y agrega los que quieras pedir.</Empty> :
      <><p className="muted">Revisa los productos, las cantidades y los precios antes de enviar.</p><div className="cart-layout"><div>
        {lines.map(l => <div className="cart-line" key={l.idInstrumento}><div><strong>{l.nombre}</strong><p className="muted small">{money(l.precioEsperado)} por unidad · {l.stock} disponibles</p><button className="text-button muted" disabled={busy || blocked} onClick={() => onRemove(l.idInstrumento)}>Quitar</button></div><label className="quantity"><span className="sr-only">Cantidad de {l.nombre}</span><input type="number" min="1" max={l.stock || 1} step="1" disabled={busy || blocked} value={l.cantidad} onChange={e => onQuantity(l.idInstrumento, e.target.value)} /></label></div>)}
        <Button variant="secondary" icon="refresh" disabled={busy || blocked} onClick={onRefresh}>Revisar precios y stock</Button>
      </div><aside className="cart-summary"><p className="eyebrow">Resumen del pedido</p><div className="summary-total"><span>Total</span><strong>{money(total)}</strong></div><p className="small muted">Precios en pesos chilenos. Las existencias se comprueban al enviar.</p><label className="check-label"><input type="checkbox" checked={reviewed} disabled={busy || blocked} onChange={e => setReviewed(e.target.checked)} /><span>Revisé productos, cantidades y precios.</span></label><Button className="full" icon="bag" disabled={busy || blocked || !!invalid || !reviewed} onClick={onSubmit}>{busy ? 'Enviando…' : 'Enviar pedido'}</Button></aside></div><Notice>{invalid}</Notice>{blocked && <Notice tone="warning">Primero consulta la solicitud que quedó pendiente.</Notice>}</>}
  </Modal>;
}
