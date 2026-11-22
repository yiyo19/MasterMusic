import React, { useRef, useState } from 'react';
import { Button, Modal, Notice } from './ui.jsx';
export function Auth({ request, onLogin, onClose }) {
  const [register, setRegister] = useState(false), [error, setError] = useState(''), [busy, setBusy] = useState(false);
  const lock = useRef(false);
  async function submit(e) {
    e.preventDefault(); if (lock.current) return;
    const form = new FormData(e.currentTarget);
    const correo = String(form.get('correo')).trim(), clave = String(form.get('clave'));
    lock.current = true; setBusy(true); setError('');
    try {
      if (register) await request('catalogo', '/api/v1.0/Auth/registro', { method: 'POST', publicRequest: true,
        body: { correo, clave, nombreCompleto: String(form.get('nombre')).trim() } });
      const { data } = await request('catalogo', '/api/v1.0/Auth/login', { method: 'POST', publicRequest: true, body: { correo, clave } });
      if (!data?.token || !['Cliente', 'Administrador'].includes(data.usuario?.rol)) throw Error('La respuesta de acceso no es válida.');
      onLogin(data);
    } catch (e) { setError(e.message); } finally { lock.current = false; setBusy(false); }
  }
  return <Modal title={register ? 'Crea tu cuenta' : 'Bienvenido de vuelta'} onClose={onClose} busy={busy}>
    <p className="muted">Accede para seleccionar instrumentos y seguir tus pedidos.</p>
    <form onSubmit={submit}><fieldset disabled={busy}>
      {register && <label>Nombre completo<input name="nombre" maxLength={150} required autoComplete="name" /></label>}
      <label>Correo electrónico<input name="correo" type="email" maxLength={150} required autoComplete="email" placeholder="tu.correo@ejemplo.cl" /></label>
      <label>Contraseña<input name="clave" type="password" required autoComplete={register ? 'new-password' : 'current-password'} /></label>
      <Notice>{error}</Notice>
      <Button className="full" type="submit">{busy ? 'Validando…' : register ? 'Crear cuenta e ingresar' : 'Ingresar'}</Button>
    </fieldset></form>
    <p className="auth-switch">{register ? '¿Ya tienes cuenta?' : '¿Primera vez aquí?'} <button className="text-button" disabled={busy} onClick={() => { setRegister(!register); setError(''); }}>{register ? 'Inicia sesión' : 'Regístrate'}</button></p>
  </Modal>;
}
