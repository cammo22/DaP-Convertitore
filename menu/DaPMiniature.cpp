// DaP Convertitore — le miniature di Esplora file (IThumbnailProvider).
//
// Esplora file chiede alla DLL «dammi l'anteprima di questo file a N pixel». La DLL non disegna niente da sola:
// lancia DaPConvertitore.exe --miniatura «file» N «uscita» (il percorso dell'exe lo scrive l'app nel registro), aspetta, e rende a Esplora file quello
// che l'exe ha disegnato: il fotogramma del video col suo tasto play, il modello 3D, la forma d'onda della musica…
// L'exe scrive due interi (larghezza, altezza) e poi i pixel BGRA premoltiplicati, riga per riga dall'alto.
//
// Gira dentro Esplora file (non in dllhost): l'app registra la classe con DisableProcessIsolation, perché un
// provider isolato riceve solo un flusso senza il percorso del file, e per FFmpeg il percorso serve.
// Perché un processo e non il disegno qui dentro: i disegni (WPF, FFmpeg, il rasterizzatore dei modelli) sono
// codice .NET, e una DLL .NET dentro Esplora file / dllhost non è una buona idea. Le miniature poi Windows le tiene in
// cache: il processo parte una volta sola per file.
//
// Si compila con Zig (scripts\compila-menu.ps1): niente Visual Studio.

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlwapi.h>
#include <shobjidl.h>
#include <thumbcache.h>
#include <new>
#include <cstdarg>

// {B7E3D9A1-4C52-4F08-9A6D-3E1F5C8B2D74}: la miniatura (la registra l'app in HKCU\Software\Classes\CLSID)
static const CLSID CLSID_Miniatura = {0xb7e3d9a1, 0x4c52, 0x4f08, {0x9a, 0x6d, 0x3e, 0x1f, 0x5c, 0x8b, 0x2d, 0x74}};

// IThumbnailProvider: l'IID non sta in nessuna libreria di MinGW, lo scriviamo noi ({e357fccd-a995-4576-b01f-234630154e96})
static const IID IID_Miniatura = {0xe357fccd, 0xa995, 0x4576, {0xb0, 0x1f, 0x23, 0x46, 0x30, 0x15, 0x4e, 0x96}};

static HMODULE g_modulo = nullptr;
static LONG g_oggetti = 0;
static LONG g_blocchi = 0;
static LONG g_contatore = 0;

static const DWORD ATTESA_MASSIMA = 30000; // ms: oltre, Esplora file si tiene l'icona

/// Il diario per capire cosa succede nel processo che la carica: si accende solo se accanto alla DLL c'è un file «miniature.debug»
/// e scrive in %LocalAppData%\DaProd\Convertitore\miniature-diario.txt.
static void Diario(const wchar_t* formato, ...)
{
    static int acceso = -1;
    wchar_t p[MAX_PATH];
    if (acceso < 0)
    {
        GetModuleFileNameW(g_modulo, p, MAX_PATH);
        PathRemoveFileSpecW(p);
        PathAppendW(p, L"miniature.debug");
        acceso = GetFileAttributesW(p) != INVALID_FILE_ATTRIBUTES ? 1 : 0;
    }
    if (!acceso) return;
    wchar_t riga[900];
    SYSTEMTIME t;
    GetLocalTime(&t);
    int k = wsprintfW(riga, L"%02d:%02d:%02d.%03d [%lu]  ", t.wHour, t.wMinute, t.wSecond, t.wMilliseconds, GetCurrentProcessId());
    va_list a;
    va_start(a, formato);
    k += wvsprintfW(riga + k, formato, a);
    va_end(a);
    lstrcatW(riga, L"\r\n");
    // accanto ai dati dell'app (%LocalAppData%\DaProd\Convertitore): il TEMP di dllhost può essere un altro
    GetEnvironmentVariableW(L"LOCALAPPDATA", p, MAX_PATH);
    PathAppendW(p, L"DaProd");
    CreateDirectoryW(p, nullptr);
    PathAppendW(p, L"Convertitore");
    CreateDirectoryW(p, nullptr);
    PathAppendW(p, L"miniature-diario.txt");
    HANDLE f = CreateFileW(p, FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_ALWAYS, 0, nullptr);
    if (f == INVALID_HANDLE_VALUE) return;
    char buf[2000];
    int n = WideCharToMultiByte(CP_UTF8, 0, riga, -1, buf, sizeof(buf), nullptr, nullptr);
    DWORD scritti;
    if (n > 1) WriteFile(f, buf, (DWORD)(n - 1), &scritti, nullptr);
    CloseHandle(f);
}

/// Quanti disegni contemporanei in tutta la macchina: aprire una cartella di mille file non deve lanciare mille
/// processi insieme. Il semaforo è uno solo per tutti i processi.
static HANDLE Semaforo()
{
    static HANDLE s = nullptr;
    if (!s) s = CreateSemaphoreW(nullptr, 3, 3, L"Local\\DaProd.Convertitore.Miniature");
    return s;
}

class Miniatura final : public IInitializeWithItem, public IThumbnailProvider
{
    LONG rif = 1;
    PWSTR percorso = nullptr;

public:
    Miniatura() { InterlockedIncrement(&g_oggetti); Diario(L"nuova miniatura"); }
    ~Miniatura() { if (percorso) CoTaskMemFree(percorso); InterlockedDecrement(&g_oggetti); }

    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        Diario(L"QI {%08lx-%04x-%04x}", riid.Data1, riid.Data2, riid.Data3);
        if (IsEqualIID(riid, IID_IUnknown) || IsEqualIID(riid, IID_IInitializeWithItem)) { *ppv = static_cast<IInitializeWithItem*>(this); AddRef(); return S_OK; }
        if (IsEqualIID(riid, IID_Miniatura)) { *ppv = static_cast<IThumbnailProvider*>(this); AddRef(); return S_OK; }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }
    IFACEMETHODIMP_(ULONG) AddRef() override { return (ULONG)InterlockedIncrement(&rif); }
    IFACEMETHODIMP_(ULONG) Release() override { LONG r = InterlockedDecrement(&rif); if (r == 0) delete this; return (ULONG)r; }

    IFACEMETHODIMP Initialize(IShellItem* item, DWORD) override
    {
        if (percorso) { CoTaskMemFree(percorso); percorso = nullptr; }
        HRESULT hr = item->GetDisplayName(SIGDN_FILESYSPATH, &percorso);
        Diario(L"Initialize hr=%lx %s", (unsigned long)hr, percorso ? percorso : L"(niente)");
        return hr;
    }

    IFACEMETHODIMP GetThumbnail(UINT cx, HBITMAP* bitmap, WTS_ALPHATYPE* alfa) override
    {
        *bitmap = nullptr;
        *alfa = WTSAT_UNKNOWN;
        Diario(L"GetThumbnail %u %s", cx, percorso ? percorso : L"(niente)");
        if (!percorso) return E_UNEXPECTED;

        // l'exe lo scrive l'app nel registro (la DLL gira da una copia fuori dalla cartella dell'app, che si può aggiornare)
        wchar_t exe[MAX_PATH] = {};
        DWORD dim = sizeof(exe);
        if (RegGetValueW(HKEY_CURRENT_USER, L"Software\\DaProd\\Convertitore", L"Exe", RRF_RT_REG_SZ, nullptr, exe, &dim) != ERROR_SUCCESS || GetFileAttributesW(exe) == INVALID_FILE_ATTRIBUTES)
        {
            GetModuleFileNameW(g_modulo, exe, MAX_PATH);
            PathRemoveFileSpecW(exe);
            PathAppendW(exe, L"DaPConvertitore.exe");
        }
        if (GetFileAttributesW(exe) == INVALID_FILE_ATTRIBUTES) { Diario(L"exe non trovato: %s", exe); return E_FAIL; }
        wchar_t cartella[MAX_PATH];
        lstrcpyW(cartella, exe);
        PathRemoveFileSpecW(cartella);

        wchar_t uscita[MAX_PATH];
        GetTempPathW(MAX_PATH, uscita);
        PathAppendW(uscita, L"DaP Convertitore");
        CreateDirectoryW(uscita, nullptr);
        wchar_t nome[64];
        wsprintfW(nome, L"min-%lu-%lu-%lu.bin", GetCurrentProcessId(), GetTickCount(), (unsigned long)InterlockedIncrement(&g_contatore));
        PathAppendW(uscita, nome);

        wchar_t riga[MAX_PATH * 4 + 128];
        wsprintfW(riga, L"\"%s\" --miniatura \"%s\" %u \"%s\"", exe, percorso, cx, uscita);

        HANDLE sem = Semaforo();
        if (sem && WaitForSingleObject(sem, 60000) != WAIT_OBJECT_0) return E_FAIL;
        HRESULT hr = Disegna(exe, cartella, riga, uscita, bitmap, alfa);
        if (sem) ReleaseSemaphore(sem, 1, nullptr);
        DeleteFileW(uscita);
        return hr;
    }

private:
    static HRESULT Disegna(const wchar_t* exe, const wchar_t* cartella, wchar_t* riga, const wchar_t* uscita, HBITMAP* bitmap, WTS_ALPHATYPE* alfa)
    {
        // un job con «chiudi tutto se muori»: se si scade il tempo, anche FFmpeg (figlio dell'exe) si ferma
        HANDLE job = CreateJobObjectW(nullptr, nullptr);
        if (job)
        {
            JOBOBJECT_EXTENDED_LIMIT_INFORMATION lim = {};
            lim.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            SetInformationJobObject(job, JobObjectExtendedLimitInformation, &lim, sizeof(lim));
        }
        STARTUPINFOW si = {};
        si.cb = sizeof(si);
        PROCESS_INFORMATION pi = {};
        if (!CreateProcessW(exe, riga, nullptr, nullptr, FALSE, CREATE_NO_WINDOW | CREATE_SUSPENDED | BELOW_NORMAL_PRIORITY_CLASS, nullptr, cartella, &si, &pi))
        {
            Diario(L"CreateProcess non riesce: errore %lu", GetLastError());
            if (job) CloseHandle(job);
            return E_FAIL;
        }
        if (job) AssignProcessToJobObject(job, pi.hProcess);
        ResumeThread(pi.hThread);
        CloseHandle(pi.hThread);
        DWORD r = WaitForSingleObject(pi.hProcess, ATTESA_MASSIMA);
        if (r != WAIT_OBJECT_0) TerminateProcess(pi.hProcess, 1);
        DWORD codice = 1;
        GetExitCodeProcess(pi.hProcess, &codice);
        CloseHandle(pi.hProcess);
        if (job) CloseHandle(job);
        Diario(L"exe finito: attesa=%lu codice=%lu", r, codice);
        if (r != WAIT_OBJECT_0 || codice != 0) return E_FAIL;

        HANDLE f = CreateFileW(uscita, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, 0, nullptr);
        if (f == INVALID_HANDLE_VALUE) return E_FAIL;
        int testa[2] = {};
        DWORD letti = 0;
        HRESULT hr = E_FAIL;
        if (ReadFile(f, testa, sizeof(testa), &letti, nullptr) && letti == sizeof(testa) && testa[0] > 0 && testa[1] > 0 && testa[0] <= 4096 && testa[1] <= 4096)
        {
            BITMAPINFO bi = {};
            bi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
            bi.bmiHeader.biWidth = testa[0];
            bi.bmiHeader.biHeight = -testa[1]; // dall'alto
            bi.bmiHeader.biPlanes = 1;
            bi.bmiHeader.biBitCount = 32;
            bi.bmiHeader.biCompression = BI_RGB;
            void* pixel = nullptr;
            HBITMAP hb = CreateDIBSection(nullptr, &bi, DIB_RGB_COLORS, &pixel, nullptr, 0);
            if (hb && pixel)
            {
                DWORD dim = (DWORD)testa[0] * (DWORD)testa[1] * 4;
                if (ReadFile(f, pixel, dim, &letti, nullptr) && letti == dim)
                {
                    *bitmap = hb;
                    *alfa = WTSAT_ARGB;
                    hr = S_OK;
                }
                else DeleteObject(hb);
            }
            else if (hb) DeleteObject(hb);
        }
        CloseHandle(f);
        return hr;
    }
};

// ————————————————————————— la fabbrica e le funzioni che COM cerca —————————————————————————

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
        Miniatura* o = new (std::nothrow) Miniatura();
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
    Diario(L"DllGetClassObject");
    if (!IsEqualCLSID(clsid, CLSID_Miniatura)) return CLASS_E_CLASSNOTAVAILABLE;
    IClassFactory* f = new (std::nothrow) Fabbrica();
    if (!f) return E_OUTOFMEMORY;
    HRESULT hr = f->QueryInterface(riid, ppv);
    f->Release();
    return hr;
}

STDAPI DllCanUnloadNow()
{
    return g_oggetti == 0 && g_blocchi == 0 ? S_OK : S_FALSE;
}
