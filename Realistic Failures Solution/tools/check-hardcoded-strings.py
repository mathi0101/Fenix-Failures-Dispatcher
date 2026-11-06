"""Validador best-effort de textos hardcodeados en la capa visible (issue #12).

Detecta olvidos gruesos de migración: atributos XAML traducibles sin {loc:Loc}
y literales user-facing en viewmodels/code-behind. Los logs (Serilog), la
terminología aeronáutica en inglés y las marcas quedan excluidos por diseño.

Uso (desde Realistic Failures Solution/):
    python tools/check-hardcoded-strings.py
"""
import glob
import re
import sys

UI = "RealFenixFailures.UI"

# Marcas/glifos/unidades que NO se traducen (decisión issue #12).
ALLOWED_XAML = re.compile(
    r"^(Real Fenix Failures|Realistic A320 Fenix Failures|Real Fenix| ?Failures|MSFS( 2020/2024)?|"
    r"Fenix System|Callsign|OFP|Profile|FPS|min|\d+x|\+[0-9]+ (min|h)|(--:--:-- ?UTC)|v)$"
)
SKIP_FILES = ("CrashLogWriter.cs", "TelemetryService.cs")


def xaml_leftovers():
    total = 0
    for f in glob.glob(f"{UI}/**/*.xaml", recursive=True):
        if "obj" in f or "bin" in f:
            continue
        s = open(f, encoding="utf-8-sig").read()
        for m in re.finditer(r'\b(Text|Header|Title|Content|ToolTip|Label)="([^"]+)"', s):
            v = m.group(2).strip()
            if not v or v.startswith("{") or "&#x" in v or "loc:Loc" in m.group(2):
                continue
            if not re.search(r"[A-Za-zÁÉÍÓÚÑáéíóúñ]", v) or ALLOWED_XAML.match(v):
                continue
            print(f"XAML  {f}: {m.group(1)}=\"{v[:60]}\"")
            total += 1
    return total


def cs_leftovers():
    total = 0
    files = (
        glob.glob(f"{UI}/ViewModels/**/*.cs", recursive=True)
        + glob.glob(f"{UI}/Views/**/*.xaml.cs", recursive=True)
        + glob.glob(f"{UI}/Services/*.cs")
        + glob.glob(f"{UI}/Diagnostics/*.cs")
        + glob.glob(f"{UI}/Localization/*.cs")
    )
    for f in files:
        if any(k in f for k in SKIP_FILES):
            continue
        for i, line in enumerate(open(f, encoding="utf-8-sig"), 1):
            ls = line.strip()
            if ls.startswith("//") or ls.startswith("///"):
                continue
            # Excluidos por diseño: logs (Serilog), excepciones internas, escritura de crash-log.
            if "_logger." in ls or "throw new" in ls or "sb.Append" in ls or "AppendLine" in ls:
                continue
            for m in re.finditer(r'"([^"]*[a-záéíóúñ]{3}[^"]*)"', ls):
                v = m.group(1)
                # heurística user-facing: texto en español con espacios, o mensaje de CrashReporter
                if re.search(r"[áéíóúñ¿¡]", v) and " " in v:
                    print(f"CS    {f}:{i}: {ls[:110]}")
                    total += 1
                break
    return total


if __name__ == "__main__":
    n = xaml_leftovers() + cs_leftovers()
    print(f"\n{n} texto(s) hardcodeado(s) sin migrar (revisar: algunos son intencionales).")
    sys.exit(0)
