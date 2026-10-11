# SortFileZ

Windows aplikacija (WPF, .NET 10, Windows 11 Fluent izgled) za sortiranje fajlova u folderu.

## Šta radi

- Skenira izabrani folder — **samo fajlove**, podfolderi se preskaču.
- Prikazuje sve tipove fajlova (ekstenzije) sa brojem i veličinom. Obrađuju se samo štiklirani tipovi.
- Načini sortiranja:
  - **Po tipu** — PDF → `PDF`, EXE → `Aplikacije`, slike → `Slike`… Naziv foldera za svaki tip menja se u tabeli i pamti se.
  - **Po datumu** — datum kreiranja, datum izmene ili današnji datum; folder po danu, mesecu ili godini; format `2026-10-11` ili `11.10.2026`.
  - **Po imenu** — po zajedničkom početku imena (`Ugovor_Ana` → `Ugovor`) ili po prvom slovu.
  - **Svaki fajl u svoj folder** — `Setup.exe` → `Setup\Setup.exe`, `Alat.zip` → `Alat\Alat.zip`. Ako folder istog imena već postoji (npr. raspakovan zip), fajl ide u njega.
  - **Sve u jedan folder** sa zadatim nazivom.
  - **Bez premeštanja** — samo preimenovanje.
- **Preimenovanje** sa štikliranjem delova: ime, datum, redni broj (početni broj, broj cifara, razdvajač, redosled).
- **Premeštanje** (podrazumevano) ili **kopiranje**, u isti ili drugi folder.
- **Pregled pre primene** — ništa se ne menja dok se ne klikne „Primeni“. Novo ime i folder mogu se ručno izmeniti u tabeli.
- **Poništavanje** poslednje operacije (log se čuva i posle zatvaranja programa).
- Duplikati se nikad ne pregaze — dodaje se ` (2)`, ` (3)`…
- Preskaču se skriveni/sistemski fajlovi, `desktop.ini`, `Thumbs.db` i privremeni Office fajlovi (`~$…`).
- Folder se može prevući mišem na prozor.
- Interfejs na **srpskom i engleskom** (meni *Jezik / Language*), promena odmah, izbor se pamti.

Podešavanja i log za poništavanje: `%LOCALAPPDATA%\SortFileZ\`

## Build

Gotov `.exe` pravi GitHub Actions (`.github/workflows/build.yml`):

- svaki push na `main` → exe u **Actions → poslednji run → Artifacts**
- tag `v1.0.0` → exe u **Releases**

```
git tag v1.0.0
git push origin v1.0.0
```

Lokalno (potreban .NET 10 SDK):

```
dotnet publish SortFileZ/SortFileZ.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```
