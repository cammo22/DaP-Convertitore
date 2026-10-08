import { defineConfig } from 'vite';
import { resolve } from 'node:path';

// Percorsi relativi: la stessa build gira dentro WebView2 (https://dap.locale/) e nel browser per le prove.
// Due pagine: la piastra del convertitore (index.html) e il lettore (lettore.html).
export default defineConfig({
  base: './',
  clearScreen: false,
  server: { port: 5181, strictPort: true },
  build: {
    target: 'es2022',
    outDir: 'dist',
    emptyOutDir: true,
    assetsInlineLimit: 0,
    chunkSizeWarningLimit: 900,
    rollupOptions: {
      input: { index: resolve(__dirname, 'index.html'), lettore: resolve(__dirname, 'lettore.html') },
    },
  },
});
