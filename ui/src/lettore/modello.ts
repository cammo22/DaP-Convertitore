// Lo studio 3D: il modello su un piano che riflette la luce, gira da solo finché non lo tocchi. Trascina per
// girarlo, rotella per avvicinarti, W per vedere i fili. GLB, GLTF, STL (le stampe 3D), OBJ, PLY, 3MF.
import { h } from '../util';
import { attesa, errore, ic, schermoIntero, type Contesto, type Vista } from './comune';

export async function monta(c: Contesto): Promise<Vista> {
  c.palco.append(attesa('Accendo le luci dello studio…'));
  const THREE = await import('three');
  const { OrbitControls } = await import('three/examples/jsm/controls/OrbitControls.js');
  const { RoomEnvironment } = await import('three/examples/jsm/environments/RoomEnvironment.js');
  if (!c.viva()) return { tasti: [], smonta() {} };
  const url = c.scheda.url;
  const est = c.scheda.estensione;

  const tela = h<HTMLCanvasElement>('canvas.m-tela');
  const box = h('div.m-studio', null, tela);
  const r = new THREE.WebGLRenderer({ canvas: tela, antialias: true, alpha: true });
  r.setPixelRatio(devicePixelRatio);
  r.toneMapping = THREE.ACESFilmicToneMapping;
  r.outputColorSpace = THREE.SRGBColorSpace;
  const scena = new THREE.Scene();
  const pmrem = new THREE.PMREMGenerator(r);
  scena.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
  const cam = new THREE.PerspectiveCamera(40, 1, 0.01, 1000);
  const luce = new THREE.DirectionalLight(0xffffff, 1.6);
  luce.position.set(3, 6, 4);
  scena.add(luce, new THREE.HemisphereLight(0xb9a6ff, 0x1a1020, 0.6));

  let oggetto: InstanceType<typeof THREE.Object3D>;
  try {
    oggetto = await carica(THREE, est, url);
  } catch (e) {
    c.palco.replaceChildren(errore('Questo modello non si apre.', (e as Error).message));
    r.dispose();
    return { tasti: [], smonta() {} };
  }
  if (!c.viva()) { r.dispose(); return { tasti: [], smonta() {} }; }
  scena.add(oggetto);

  // in mezzo, appoggiato a terra, a misura di inquadratura
  const b = new THREE.Box3().setFromObject(oggetto);
  const dim = b.getSize(new THREE.Vector3());
  const centro = b.getCenter(new THREE.Vector3());
  const lato = Math.max(dim.x, dim.y, dim.z) || 1;
  oggetto.position.sub(new THREE.Vector3(centro.x, b.min.y, centro.z));
  const griglia = new THREE.GridHelper(lato * 4, 24, 0xff3df2, 0x2a2633);
  (griglia.material as InstanceType<typeof THREE.Material>).transparent = true;
  (griglia.material as InstanceType<typeof THREE.Material>).opacity = 0.35;
  scena.add(griglia);
  const ctrl = new OrbitControls(cam, tela);
  ctrl.enableDamping = true;
  ctrl.autoRotate = true;
  ctrl.autoRotateSpeed = 1.2;
  const inquadra = () => {
    cam.position.set(lato * 1.4, lato * 0.9, lato * 1.8);
    ctrl.target.set(0, dim.y / 2, 0);
    cam.near = lato / 200; cam.far = lato * 100;
    cam.updateProjectionMatrix();
  };
  inquadra();
  tela.addEventListener('pointerdown', () => { ctrl.autoRotate = false; });

  let fili = false;
  const materiali: { wireframe: boolean }[] = [];
  let triangoli = 0;
  oggetto.traverse((o) => {
    const m = (o as unknown as { material?: { wireframe: boolean } | { wireframe: boolean }[] }).material;
    if (m) materiali.push(...(Array.isArray(m) ? m : [m]));
    const g = (o as unknown as { geometry?: InstanceType<typeof THREE.BufferGeometry> }).geometry;
    if (g) triangoli += (g.index ? g.index.count : g.attributes.position?.count ?? 0) / 3;
  });

  c.palco.replaceChildren(box, h('div.m-aiuto', null, 'Trascina per girare · rotella per avvicinarti · tasto destro per spostare'));
  const misura = () => { const w = box.clientWidth, hh = box.clientHeight; r.setSize(w, hh, false); cam.aspect = w / hh; cam.updateProjectionMatrix(); };
  const ro = new ResizeObserver(misura);
  ro.observe(box);
  misura();
  let giro = 0;
  const anima = () => { giro = requestAnimationFrame(anima); ctrl.update(); r.render(scena, cam); };
  anima();

  return {
    tasti: [
      { k: [' '], etichetta: 'Spazio', cosa: 'Gira da solo / fermo', fai: () => { ctrl.autoRotate = !ctrl.autoRotate; } },
      { k: ['w'], etichetta: 'W', cosa: 'Solo i fili', fai: () => { fili = !fili; materiali.forEach((m) => (m.wireframe = fili)); c.hud(fili ? 'Fili' : 'Pieno', ic.cubo); } },
      { k: ['g'], etichetta: 'G', cosa: 'Pavimento a griglia', fai: () => { griglia.visible = !griglia.visible; } },
      { k: ['0', 'home'], etichetta: '0', cosa: 'Inquadra di nuovo', fai: () => { inquadra(); ctrl.autoRotate = true; } },
      { k: ['f'], etichetta: 'F', cosa: 'Schermo intero', fai: schermoIntero },
    ],
    barraSopra: true,
    info: () => `${Math.round(triangoli).toLocaleString('it-IT')} triangoli · ${dim.x.toFixed(1)} × ${dim.y.toFixed(1)} × ${dim.z.toFixed(1)}`,
    smonta() {
      cancelAnimationFrame(giro);
      ro.disconnect();
      ctrl.dispose();
      pmrem.dispose();
      r.dispose();
    },
  };
}

async function carica(THREE: typeof import('three'), est: string, url: string) {
  const materiale = () => new THREE.MeshStandardMaterial({ color: 0xd9d4e6, metalness: 0.15, roughness: 0.45 });
  switch (est) {
    case '.glb':
    case '.gltf': {
      const { GLTFLoader } = await import('three/examples/jsm/loaders/GLTFLoader.js');
      return (await new GLTFLoader().loadAsync(url)).scene;
    }
    case '.stl': {
      const { STLLoader } = await import('three/examples/jsm/loaders/STLLoader.js');
      const g = await new STLLoader().loadAsync(url);
      g.computeVertexNormals();
      const m = new THREE.Mesh(g, materiale());
      m.rotation.x = -Math.PI / 2; // le stampe 3D hanno la Z in alto
      return new THREE.Group().add(m);
    }
    case '.obj': {
      const { OBJLoader } = await import('three/examples/jsm/loaders/OBJLoader.js');
      const o = await new OBJLoader().loadAsync(url);
      o.traverse((x) => { const m = x as InstanceType<typeof THREE.Mesh>; if (m.isMesh && !(m.material as { map?: unknown }).map) m.material = materiale(); });
      return o;
    }
    case '.ply': {
      const { PLYLoader } = await import('three/examples/jsm/loaders/PLYLoader.js');
      const g = await new PLYLoader().loadAsync(url);
      g.computeVertexNormals();
      const mat = g.attributes.color ? new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.6 }) : materiale();
      return new THREE.Group().add(new THREE.Mesh(g, mat));
    }
    case '.3mf': {
      const { ThreeMFLoader } = await import('three/examples/jsm/loaders/3MFLoader.js');
      const o = await new ThreeMFLoader().loadAsync(url);
      o.rotation.x = -Math.PI / 2;
      return new THREE.Group().add(o);
    }
  }
  throw new Error('formato che non conosco');
}

