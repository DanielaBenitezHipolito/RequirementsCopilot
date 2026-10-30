import { StrictMode } from 'react';

// Tras un deploy, los chunks con hash viejo desaparecen del servidor: si la pestaña quedó con la
// versión anterior, el import dinámico falla. Se recarga una sola vez para tomar el build nuevo.
window.addEventListener('vite:preloadError', (event) => {
  if (!sessionStorage.getItem('rc_reloaded')) {
    sessionStorage.setItem('rc_reloaded', '1');
    event.preventDefault();
    window.location.reload();
  }
});
window.addEventListener('load', () => sessionStorage.removeItem('rc_reloaded'));

import { createRoot } from 'react-dom/client';
import './styles/index.css';
import App from './App';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
