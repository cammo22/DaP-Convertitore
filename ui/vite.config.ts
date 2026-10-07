import { defineConfig } from 'vite';

// Percorsi relativi: la stessa build gira dentro WebView2 (https://dap.locale/) e nel browser per le prove.
export default defineConfig({
  base: './',
  clearScreen: false,
  server: { port: 5181, strictPort: true },
  build: {
    target: 'es2022',
    outDir: 'dist',
    emptyOutDir: true,
    assetsInlineLimit: 0,
  },
});
