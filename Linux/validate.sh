#!/usr/bin/env bash
set -euo pipefail

linux_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
case "${1:-}" in
  "")
    dotnet build "$linux_dir/ResetExplode.csproj" -c Release --disable-build-servers -m:1
    dotnet build "$linux_dir/SmokeTests/ResetExplode.SmokeTests.csproj" -c Release --disable-build-servers -m:1
    dotnet run --project "$linux_dir/SmokeTests/ResetExplode.SmokeTests.csproj" -c Release --no-build
    ;;
  --offline-net8)
    # Compila os projetos originais e suas referências; não inclui Avalonia.
    validation_dir="$(mktemp -d "${TMPDIR:-/tmp}/reset-validation.XXXXXXXX")"
    trap 'rm -rf -- "$validation_dir"' EXIT
    cat > "$validation_dir/NuGet.Config" <<'XML'
<configuration><packageSources><clear /></packageSources></configuration>
XML
    dotnet build "$linux_dir/SmokeTests/ResetExplode.SmokeTests.csproj" -c Release \
      --disable-build-servers -m:1 --configfile "$validation_dir/NuGet.Config" \
      -p:EnableTargetingPackDownload=false
    dotnet "$linux_dir/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll"
    echo 'Projetos Core/Hardware/SmokeTests validados em .NET 8; Avalonia NÃO foi validado por este modo.'
    ;;
  --offline-net10)
    # Validação suplementar: não compila Avalonia nem muda o alvo dos projetos reais.
    validation_dir="$(mktemp -d "${TMPDIR:-/tmp}/reset-validation.XXXXXXXX")"
    trap 'rm -rf -- "$validation_dir"' EXIT
    python3 - "$linux_dir" "$validation_dir" <<'PY'
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
linux, temporary = map(Path, sys.argv[1:])
project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
props = ET.SubElement(project, 'PropertyGroup')
for key, value in {
    'OutputType': 'Exe', 'TargetFramework': 'net10.0', 'Nullable': 'enable',
    'ImplicitUsings': 'enable', 'EnableDefaultCompileItems': 'false',
    'UseAppHost': 'false', 'EnableTargetingPackDownload': 'false'
}.items():
    ET.SubElement(props, key).text = value
items = ET.SubElement(project, 'ItemGroup')
sources = [linux.parent / 'Shared/Benchmarks/BenchRenderer.cs', linux.parent / 'Shared/Benchmarks/GameSimBench.cs']
for folder in ('Core', 'Hardware', 'SmokeTests'):
    sources.extend(path for path in (linux / folder).rglob('*.cs') if not {'bin', 'obj'}.intersection(path.relative_to(linux / folder).parts))
for path in sources:
    ET.SubElement(items, 'Compile', Include=str(path))
ET.ElementTree(project).write(temporary / 'Checks.csproj', encoding='unicode')
(temporary / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
PY
    dotnet build "$validation_dir/Checks.csproj" -c Release --disable-build-servers -m:1
    dotnet "$validation_dir/bin/Release/net10.0/Checks.dll"
    echo 'Validação suplementar .NET 10 concluída; Avalonia e o alvo .NET 8 NÃO foram validados por este modo.'
    ;;
  *)
    echo 'Uso: ./Linux/validate.sh [--offline-net8|--offline-net10]' >&2
    exit 2
    ;;
esac
