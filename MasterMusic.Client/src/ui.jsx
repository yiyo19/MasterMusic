import React, { useEffect, useRef } from 'react';
export function Icon({ name = 'music', size = 22, ...rest }) {
  const paths = {
    music: <><path d="M9 18V5l12-2v13M9 9l12-2" /><ellipse cx="6" cy="18" rx="3" ry="2" /><ellipse cx="18" cy="16" rx="3" ry="2" /></>,
    bag: <><path d="M5 7h14l1 14H4L5 7Z" /><path d="M9 8V6a3 3 0 0 1 6 0v2" /></>,
    search: <><circle cx="10" cy="10" r="6" /><path d="m15 15 6 6" /></>,
    box: <><path d="m12 2 9 5v10l-9 5-9-5V7l9-5Z M3 7l9 5 9-5 M12 12v10 M7 4.8l9 5" /></>,
    check: <path d="m4 12 5 5L20 6" />,
    plus: <path d="M12 4v16M4 12h16" />,
    close: <path d="m6 6 12 12M18 6 6 18" />,
    refresh: <><path d="M20 7V3m0 4h-4M4 17v4m0-4h4" /><path d="M5 8a8 8 0 0 1 15-1M4 17a8 8 0 0 0 15-1" /></>,
    user: <><circle cx="12" cy="8" r="4" /><path d="M4 22v-3a8 8 0 0 1 16 0v3" /></>,
    edit: <><path d="m4 16 12-12 4 4L8 20H4v-4Z M13 7l4 4" /></>
  };
  return <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.65" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" {...rest}>{paths[name] || paths.music}</svg>;
}
export function Button({ children, variant = 'primary', icon, className = '', ...props }) {
  return <button className={'button ' + variant + ' ' + className} {...props}>{icon && <Icon name={icon} size={18} />}{children}</button>;
}
export function Notice({ children, tone = 'error' }) {
  return children ? <div className={'notice ' + tone} role={tone === 'error' ? 'alert' : 'status'}>{children}</div> : null;
}
export function Empty({ title, children, action, icon = 'box' }) {
  return <div className="empty"><span className="empty-icon"><Icon name={icon} size={32} /></span><h3>{title}</h3><p>{children}</p>{action}</div>;
}
export function Loading({ children = 'Cargando…' }) {
  return <div className="loading" role="status"><span className="spinner" />{children}</div>;
}
export function Status({ value }) {
  return <span className={'status status-' + value.toLowerCase()}><span />{value}</span>;
}
export function Modal({ title, children, onClose, busy = false, wide = false }) {
  const ref = useRef(null);
  useEffect(() => {
    const dialog = ref.current;
    const previous = document.activeElement;
    if (!dialog.open) dialog.showModal();
    return () => { dialog.close(); previous?.focus?.(); };
  }, []);
  return <dialog ref={ref} className={'modal ' + (wide ? 'wide' : '')} onCancel={e => { e.preventDefault(); if (!busy) onClose(); }}
    aria-labelledby="modal-title">
    <div className="modal-head"><h2 id="modal-title">{title}</h2><button aria-label="Cerrar ventana" className="icon-button" disabled={busy} onClick={onClose}><Icon name="close" /></button></div>
    {children}
  </dialog>;
}
export function Photo({ item, large = false }) {
  const source = /^https?:\/\//i.test(item.foto || '') ? item.foto : null;
  const [broken, setBroken] = React.useState(false);
  return <div className={'product-photo ' + (large ? 'large' : '')}>
    {source && !broken ? <img src={source} alt={item.nombre} loading="lazy" referrerPolicy="no-referrer" onError={() => setBroken(true)} />
      : <><div className="photo-lines" /><Icon name="music" size={large ? 90 : 64} /><span>{item.nombreCategoria || 'MasterMusic'}</span></>}
  </div>;
}
