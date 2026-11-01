// Aplica el tema antes de pintar para evitar el "parpadeo" claro/oscuro.
try {
  var t = localStorage.getItem('urb-theme');
  if (t === 'dark' || (!t && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
    document.documentElement.classList.add('dark');
  }
} catch (e) {
  /* almacenamiento no disponible: se usa el tema claro */
}
