import React from 'react';
import { createRoot } from 'react-dom/client';
import App from './App.jsx';
import './styles.css';

class ErrorBoundary extends React.Component {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  render() {
    if (this.state.failed) return <main className="fatal-error"><h1>No pudimos mostrar la pantalla</h1><p>Recarga la página. Si habías enviado un pedido, vuelve a ingresar y consulta su resultado antes de repetirlo.</p><button className="button primary" onClick={() => location.reload()}>Recargar</button></main>;
    return this.props.children;
  }
}
createRoot(document.getElementById('root')).render(<ErrorBoundary><App /></ErrorBoundary>);
