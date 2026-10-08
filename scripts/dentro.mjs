// Esegue JavaScript dentro la pagina dell'app vera (WebView2 col debug remoto acceso).
// Uso: avvia l'app con WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9333
//      poi: node scripts/dentro.mjs "espressione"
const porta = process.env.DAP_PORTA ?? '9333';
const pagine = await (await fetch(`http://127.0.0.1:${porta}/json`)).json();
const pagina = pagine.find((p) => p.type === 'page' && p.url.includes('dap.locale'));
if (!pagina) { console.error('pagina non trovata', pagine.map((p) => p.url)); process.exit(1); }
const ws = new WebSocket(pagina.webSocketDebuggerUrl);
await new Promise((ok) => ws.addEventListener('open', ok));
ws.send(JSON.stringify({ id: 1, method: 'Runtime.evaluate', params: { expression: process.argv[2], awaitPromise: true, returnByValue: true, replMode: true } }));
ws.addEventListener('message', (e) => {
  const m = JSON.parse(e.data);
  if (m.id !== 1) return;
  console.log(JSON.stringify(m.result?.result?.value ?? m.result, null, 2));
  ws.close();
});
