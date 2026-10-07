// DaP Convertitore — il tasto destro di Windows 11 (quello nuovo, senza «Mostra altre opzioni").
//
// Windows 11 nel menu nuovo mette solo comandi IExplorerCommand registrati da un pacchetto con identità: questa DLL
// è il comando, menu\AppxManifest.xml il pacchetto (sparse: i file restano nella cartella dell'app). Due voci:
//   «DaP Convertitore»      → apre la piastra con i file scelti
//   «Converti al volo  ›»   → sottomenu con le conversioni rapide della categoria del file
// Cosa c'è nel sottomenu non sta qui: lo scrive l'app in menu.tsv accanto alla DLL, leggendo il Catalogo.
// Una cosa sola, uguale ovunque. Clic → si lancia DaPConvertitore.exe con tutti i file in un colpo solo.
//
// Si compila con Zig (scripts\compila-menu.ps1): niente Visual Studio. Gira in dllhost (SurrogateServer).

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlwapi.h>
#include <shobjidl.h>
#include <shlobj.h>
#include <new>

// {9C1D5F3A-7B21-4E58-A6F2-3D8E0B4C61A1} apri, {5E2A8C47-1F93-4B6D-8E0A-72C4D9B135F2} converti
static const CLSID CLSID_Apri = {0x9c1d5f3a, 0x7b21, 0x4e58, {0xa6, 0xf2, 0x3d, 0x8e, 0x0b, 0x4c, 0x61, 0xa1}};
static const CLSID CLSID_Converti = {0x5e2a8c47, 0x1f93, 0x4b6d, {0x8e, 0x0a, 0x72, 0xc4, 0xd9, 0xb1, 0x35, 0xf2}};

static HMODULE g_modulo = nullptr;
static LONG g_oggetti = 0;
static LONG g_blocchi = 0;

// ————————————————————————— menu.tsv: le estensioni e le voci, scritte dall'app —————————————————————————

struct Voce { wchar_t categoria[24]; wchar_t id[40]; wchar_t etichetta[96]; };
struct Estensione { wchar_t est[16]; wchar_t categoria[24]; };

static Voce g_voci[96];
static int g_nVoci = 0;
static Estensione g_est[320];
static int g_nEst = 0;
static wchar_t g_titoloApri[96] = L"DaP Convertitore";
static wchar_t g_titoloConverti[96] = L"Converti al volo";
static bool g_letto = false;

static void Cartella(wchar_t* dove, DWORD max)
{
    GetModuleFileNameW(g_modulo, dove, max);
    PathRemoveFileSpecW(dove);
}

static void Copia(wchar_t* dest, size_t max, const wchar_t* da, size_t n)
{
    size_t i = 0;
    for (; i < n && i + 1 < max; i++) dest[i] = da[i];
    dest[i] = 0;
}

static void LeggiMenu()
{
    if (g_letto) return;
    g_letto = true;
    wchar_t percorso[MAX_PATH];
    Cartella(percorso, MAX_PATH);
    PathAppendW(percorso, L"menu.tsv");
    HANDLE f = CreateFileW(percorso, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, 0, nullptr);
    if (f == INVALID_HANDLE_VALUE) return;
    DWORD dim = GetFileSize(f, nullptr), letti = 0;
    if (dim == 0 || dim > 512 * 1024) { CloseHandle(f); return; }
    char* byte_ = (char*)HeapAlloc(GetProcessHeap(), 0, dim);
    ReadFile(f, byte_, dim, &letti, nullptr);
    CloseHandle(f);
    int n = MultiByteToWideChar(CP_UTF8, 0, byte_, (int)letti, nullptr, 0);
    wchar_t* t = (wchar_t*)HeapAlloc(GetProcessHeap(), 0, (n + 1) * sizeof(wchar_t));
    MultiByteToWideChar(CP_UTF8, 0, byte_, (int)letti, t, n);
    t[n] = 0;
    HeapFree(GetProcessHeap(), 0, byte_);

    // righe:  E <tab> .mp4 <tab> video   |   V <tab> video <tab> video-mp4 <tab> MP4 · va ovunque   |   T <tab> apri <tab> titolo
    wchar_t* riga = t;
    while (*riga)
    {
        wchar_t* fine = riga;
        while (*fine && *fine != L'\n') fine++;
        wchar_t* campi[4] = {};
        size_t lun[4] = {};
        int nc = 0;
        wchar_t* p = riga;
        while (p < fine && nc < 4)
        {
            wchar_t* q = p;
            while (q < fine && *q != L'\t' && *q != L'\r') q++;
            campi[nc] = p; lun[nc] = (size_t)(q - p); nc++;
            if (q >= fine || *q == L'\r') break;
            p = q + 1;
        }
        if (nc >= 3 && lun[0] == 1)
        {
            if (campi[0][0] == L'E' && g_nEst < 320)
            {
                Copia(g_est[g_nEst].est, 16, campi[1], lun[1]);
                Copia(g_est[g_nEst].categoria, 24, campi[2], lun[2]);
                g_nEst++;
            }
            else if (campi[0][0] == L'V' && nc >= 4 && g_nVoci < 96)
            {
                Copia(g_voci[g_nVoci].categoria, 24, campi[1], lun[1]);
                Copia(g_voci[g_nVoci].id, 40, campi[2], lun[2]);
                Copia(g_voci[g_nVoci].etichetta, 96, campi[3], lun[3]);
                g_nVoci++;
            }
            else if (campi[0][0] == L'T')
            {
                if (lun[1] == 4 && wcsncmp(campi[1], L"apri", 4) == 0) Copia(g_titoloApri, 96, campi[2], lun[2]);
                else if (lun[1] == 8 && wcsncmp(campi[1], L"converti", 8) == 0) Copia(g_titoloConverti, 96, campi[2], lun[2]);
            }
        }
        riga = *fine ? fine + 1 : fine;
    }
    HeapFree(GetProcessHeap(), 0, t);
}

/// La categoria della selezione: quella del primo elemento (l'app poi tiene solo i file adatti).
static void CategoriaDi(IShellItemArray* sel, wchar_t* categoria, size_t max)
{
    lstrcpynW(categoria, L"", (int)max);
    if (!sel) return;
    DWORD n = 0;
    if (FAILED(sel->GetCount(&n)) || n == 0) return;
    IShellItem* item = nullptr;
    if (FAILED(sel->GetItemAt(0, &item))) return;
    LPWSTR percorso = nullptr;
    if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &percorso)) && percorso)
    {
        DWORD attr = GetFileAttributesW(percorso);
        if (attr != INVALID_FILE_ATTRIBUTES && (attr & FILE_ATTRIBUTE_DIRECTORY)) lstrcpynW(categoria, L"cartella", (int)max);
        else
        {
            // .tar.gz & co. sono archivi
            const wchar_t* doppie[] = {L".tar.gz", L".tar.xz", L".tar.bz2", L".tar.zst"};
            size_t lp = wcslen(percorso);
            for (auto d : doppie)
            {
                size_t ld = wcslen(d);
                if (lp > ld && CompareStringOrdinal(percorso + lp - ld, (int)ld, d, (int)ld, TRUE) == CSTR_EQUAL) { lstrcpynW(categoria, L"archivio", (int)max); break; }
            }
            if (!categoria[0])
            {
                const wchar_t* est = PathFindExtensionW(percorso);
                lstrcpynW(categoria, L"altro", (int)max);
                for (int i = 0; i < g_nEst; i++)
                    if (CompareStringOrdinal(est, -1, g_est[i].est, -1, TRUE) == CSTR_EQUAL) { lstrcpynW(categoria, g_est[i].categoria, (int)max); break; }
            }
        }
        CoTaskMemFree(percorso);
    }
    item->Release();
}

/// DaPConvertitore.exe <argomenti> --lista <file con un percorso per riga>: niente limite di lunghezza della riga di comando.
static HRESULT Lancia(IShellItemArray* sel, const wchar_t* argomenti)
{
    if (!sel) return E_INVALIDARG;
    DWORD n = 0;
    sel->GetCount(&n);
    wchar_t temp[MAX_PATH], lista[MAX_PATH];
    GetTempPathW(MAX_PATH, temp);
    PathAppendW(temp, L"DaP Convertitore");
    CreateDirectoryW(temp, nullptr);
    wsprintfW(lista, L"%s\\lista-%lu-%lu.txt", temp, GetCurrentProcessId(), GetTickCount());
    HANDLE f = CreateFileW(lista, GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_TEMPORARY, nullptr);
    if (f == INVALID_HANDLE_VALUE) return HRESULT_FROM_WIN32(GetLastError());
    for (DWORD i = 0; i < n; i++)
    {
        IShellItem* item = nullptr;
        if (FAILED(sel->GetItemAt(i, &item))) continue;
        LPWSTR p = nullptr;
        if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &p)) && p)
        {
            char buf[4 * MAX_PATH + 8];
            int k = WideCharToMultiByte(CP_UTF8, 0, p, -1, buf, sizeof(buf) - 2, nullptr, nullptr);
            if (k > 0) { buf[k - 1] = '\n'; DWORD scritti; WriteFile(f, buf, (DWORD)k, &scritti, nullptr); }
            CoTaskMemFree(p);
        }
        item->Release();
    }
    CloseHandle(f);

    wchar_t exe[MAX_PATH];
    Cartella(exe, MAX_PATH);
    PathAppendW(exe, L"DaPConvertitore.exe");
    wchar_t riga[3 * MAX_PATH + 128];
    wsprintfW(riga, L"\"%s\" %s --lista \"%s\"", exe, argomenti, lista);
    STARTUPINFOW si = {sizeof(si)};
    PROCESS_INFORMATION pi = {};
    if (!CreateProcessW(exe, riga, nullptr, nullptr, FALSE, 0, nullptr, nullptr, &si, &pi)) return HRESULT_FROM_WIN32(GetLastError());
    // la finestra nuova deve poter venire davanti
    AllowSetForegroundWindow(pi.dwProcessId);
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return S_OK;
}

static HRESULT Icona(LPWSTR* uscita)
{
    wchar_t exe[MAX_PATH + 4];
    Cartella(exe, MAX_PATH);
    PathAppendW(exe, L"DaPConvertitore.exe");
    lstrcatW(exe, L",0");
    return SHStrDupW(exe, uscita);
}

// ————————————————————————— i comandi —————————————————————————

class Base : public IExplorerCommand
{
protected:
    LONG rif = 1;
public:
    Base() { InterlockedIncrement(&g_oggetti); }
    virtual ~Base() { InterlockedDecrement(&g_oggetti); }
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        if (!ppv) return E_POINTER;
        if (IsEqualIID(riid, IID_IUnknown) || IsEqualIID(riid, IID_IExplorerCommand)) { *ppv = static_cast<IExplorerCommand*>(this); AddRef(); return S_OK; }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }
    IFACEMETHODIMP_(ULONG) AddRef() override { return (ULONG)InterlockedIncrement(&rif); }
    IFACEMETHODIMP_(ULONG) Release() override { LONG r = InterlockedDecrement(&rif); if (r == 0) delete this; return (ULONG)r; }
    IFACEMETHODIMP GetToolTip(IShellItemArray*, LPWSTR* t) override { *t = nullptr; return E_NOTIMPL; }
    IFACEMETHODIMP GetCanonicalName(GUID* g) override { *g = GUID_NULL; return S_OK; }
    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** e) override { *e = nullptr; return E_NOTIMPL; }
};

/// Una voce del sottomenu: una conversione al volo.
class Rapida final : public Base
{
    wchar_t id[40], etichetta[96];
public:
    Rapida(const Voce& v) { lstrcpynW(id, v.id, 40); lstrcpynW(etichetta, v.etichetta, 96); }
    IFACEMETHODIMP GetTitle(IShellItemArray*, LPWSTR* t) override { return SHStrDupW(etichetta, t); }
    IFACEMETHODIMP GetIcon(IShellItemArray*, LPWSTR* i) override { *i = nullptr; return E_NOTIMPL; }
    IFACEMETHODIMP GetState(IShellItemArray*, BOOL, EXPCMDSTATE* s) override { *s = ECS_ENABLED; return S_OK; }
    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* f) override { *f = ECF_DEFAULT; return S_OK; }
    IFACEMETHODIMP Invoke(IShellItemArray* sel, IBindCtx*) override
    {
        wchar_t arg[64];
        wsprintfW(arg, L"--azione %s", id);
        return Lancia(sel, arg);
    }
};

class Elenco final : public IEnumExplorerCommand
{
    LONG rif = 1;
    IExplorerCommand* voci[96] = {};
    ULONG n = 0, pos = 0;
public:
    Elenco(const wchar_t* categoria)
    {
        InterlockedIncrement(&g_oggetti);
        for (int i = 0; i < g_nVoci && n < 96; i++)
            if (lstrcmpiW(g_voci[i].categoria, categoria) == 0) voci[n++] = new (std::nothrow) Rapida(g_voci[i]);
    }
    ~Elenco() { for (ULONG i = 0; i < n; i++) if (voci[i]) voci[i]->Release(); InterlockedDecrement(&g_oggetti); }
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        if (!ppv) return E_POINTER;
        if (IsEqualIID(riid, IID_IUnknown) || IsEqualIID(riid, IID_IEnumExplorerCommand)) { *ppv = static_cast<IEnumExplorerCommand*>(this); AddRef(); return S_OK; }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }
    IFACEMETHODIMP_(ULONG) AddRef() override { return (ULONG)InterlockedIncrement(&rif); }
    IFACEMETHODIMP_(ULONG) Release() override { LONG r = InterlockedDecrement(&rif); if (r == 0) delete this; return (ULONG)r; }
    IFACEMETHODIMP Next(ULONG chiesti, IExplorerCommand** out, ULONG* dati) override
    {
        ULONG k = 0;
        while (k < chiesti && pos < n) { out[k] = voci[pos++]; if (out[k]) { out[k]->AddRef(); k++; } }
        if (dati) *dati = k;
        return k == chiesti ? S_OK : S_FALSE;
    }
    IFACEMETHODIMP Skip(ULONG c) override { pos = pos + c > n ? n : pos + c; return pos < n ? S_OK : S_FALSE; }
    IFACEMETHODIMP Reset() override { pos = 0; return S_OK; }
    IFACEMETHODIMP Clone(IEnumExplorerCommand** e) override { *e = nullptr; return E_NOTIMPL; }
};

/// «DaP Convertitore»: apre la piastra.
class Apri final : public Base
{
public:
    IFACEMETHODIMP GetTitle(IShellItemArray*, LPWSTR* t) override { LeggiMenu(); return SHStrDupW(g_titoloApri, t); }
    IFACEMETHODIMP GetIcon(IShellItemArray*, LPWSTR* i) override { return Icona(i); }
    IFACEMETHODIMP GetState(IShellItemArray* sel, BOOL, EXPCMDSTATE* s) override
    {
        DWORD n = 0;
        *s = sel && SUCCEEDED(sel->GetCount(&n)) && n > 0 ? ECS_ENABLED : ECS_HIDDEN;
        return S_OK;
    }
    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* f) override { *f = ECF_DEFAULT; return S_OK; }
    IFACEMETHODIMP Invoke(IShellItemArray* sel, IBindCtx*) override { return Lancia(sel, L"--apri"); }
};

/// «Converti al volo ›»: il sottomenu della categoria del file.
class Converti final : public Base
{
    wchar_t categoria[24] = L"";
public:
    IFACEMETHODIMP GetTitle(IShellItemArray*, LPWSTR* t) override { LeggiMenu(); return SHStrDupW(g_titoloConverti, t); }
    IFACEMETHODIMP GetIcon(IShellItemArray*, LPWSTR* i) override { return Icona(i); }
    IFACEMETHODIMP GetState(IShellItemArray* sel, BOOL, EXPCMDSTATE* s) override
    {
        LeggiMenu();
        CategoriaDi(sel, categoria, 24);
        bool c = false;
        for (int i = 0; i < g_nVoci && !c; i++) c = lstrcmpiW(g_voci[i].categoria, categoria) == 0;
        *s = c ? ECS_ENABLED : ECS_HIDDEN;
        return S_OK;
    }
    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* f) override { *f = ECF_HASSUBCOMMANDS; return S_OK; }
    IFACEMETHODIMP Invoke(IShellItemArray*, IBindCtx*) override { return E_NOTIMPL; }
    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** e) override
    {
        LeggiMenu();
        auto el = new (std::nothrow) Elenco(categoria);
        if (!el) return E_OUTOFMEMORY;
        *e = el;
        return S_OK;
    }
};

// ————————————————————————— la fabbrica e le funzioni che COM cerca —————————————————————————

template <class T>
class Fabbrica final : public IClassFactory
{
    LONG rif = 1;
public:
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        if (IsEqualIID(riid, IID_IUnknown) || IsEqualIID(riid, IID_IClassFactory)) { *ppv = static_cast<IClassFactory*>(this); AddRef(); return S_OK; }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }
    IFACEMETHODIMP_(ULONG) AddRef() override { return (ULONG)InterlockedIncrement(&rif); }
    IFACEMETHODIMP_(ULONG) Release() override { LONG r = InterlockedDecrement(&rif); if (r == 0) delete this; return (ULONG)r; }
    IFACEMETHODIMP CreateInstance(IUnknown* esterno, REFIID riid, void** ppv) override
    {
        *ppv = nullptr;
        if (esterno) return CLASS_E_NOAGGREGATION;
        T* o = new (std::nothrow) T();
        if (!o) return E_OUTOFMEMORY;
        HRESULT hr = o->QueryInterface(riid, ppv);
        o->Release();
        return hr;
    }
    IFACEMETHODIMP LockServer(BOOL b) override { if (b) InterlockedIncrement(&g_blocchi); else InterlockedDecrement(&g_blocchi); return S_OK; }
};

extern "C" BOOL WINAPI DllMain(HINSTANCE h, DWORD perche, LPVOID)
{
    if (perche == DLL_PROCESS_ATTACH) { g_modulo = h; DisableThreadLibraryCalls(h); }
    return TRUE;
}

STDAPI DllGetClassObject(REFCLSID clsid, REFIID riid, void** ppv)
{
    *ppv = nullptr;
    IClassFactory* f = nullptr;
    if (IsEqualCLSID(clsid, CLSID_Apri)) f = new (std::nothrow) Fabbrica<Apri>();
    else if (IsEqualCLSID(clsid, CLSID_Converti)) f = new (std::nothrow) Fabbrica<Converti>();
    else return CLASS_E_CLASSNOTAVAILABLE;
    if (!f) return E_OUTOFMEMORY;
    HRESULT hr = f->QueryInterface(riid, ppv);
    f->Release();
    return hr;
}

STDAPI DllCanUnloadNow()
{
    return g_oggetti == 0 && g_blocchi == 0 ? S_OK : S_FALSE;
}
