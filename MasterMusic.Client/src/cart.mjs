export const MAX_INT = 2147483647;
export function quantity(value) {
  const n = Number(value);
  if (!Number.isSafeInteger(n) || n < 1 || n > MAX_INT) throw Error('Indica una cantidad entera mayor a cero.');
  return n;
}
export function totalCart(lines) {
  const total = lines.reduce((sum, l) => sum + quantity(l.cantidad) * quantity(l.precioEsperado), 0);
  if (!Number.isSafeInteger(total)) throw Error('El total supera el máximo permitido.');
  return total;
}
export function orderBody(lines) {
  if (!lines.length) throw Error('Agrega un instrumento a tu selección.');
  const ids = new Set();
  const body = { lineas: lines.map(l => {
    const idInstrumento = quantity(l.idInstrumento);
    if (ids.has(idInstrumento)) throw Error('Hay instrumentos repetidos.');
    ids.add(idInstrumento);
    return { idInstrumento, cantidad: quantity(l.cantidad), precioEsperado: quantity(l.precioEsperado) };
  }) };
  totalCart(body.lineas);
  return body;
}
export function reconcileCart(lines, catalog) {
  return lines.map(l => {
    const actual = catalog.find(i => i.idInstrumento === l.idInstrumento && i.activo);
    return { ...l, precioEsperado: actual?.precio ?? l.precioEsperado,
      nombre: actual?.nombre ?? l.nombre, stock: actual?.stock ?? 0, disponible: !!actual };
  });
}
export const money = n => new Intl.NumberFormat('es-CL', { style: 'currency', currency: 'CLP', maximumFractionDigits: 0 }).format(n);
export const date = s => new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(s));
