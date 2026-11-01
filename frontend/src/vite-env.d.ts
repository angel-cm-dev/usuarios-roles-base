/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Base de la API. Por defecto "/api" (mismo origen vía proxy de Vite o nginx). */
  readonly VITE_API_BASE_URL?: string;
  /** "false" oculta el enlace al buzón de desarrollo en el menú. */
  readonly VITE_SHOW_DEV_MAILBOX?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
