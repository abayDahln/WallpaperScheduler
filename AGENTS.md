# AGENTS.md

## Overview
WinUI 3 (Windows App SDK) desktop app for scheduled wallpaper rotation on Windows 11.
- Target framework: `net8.0-windows10.0.19041.0` (unpackaged WinUI 3)
- Platform targets: `x86`, `x64`, `ARM64`

## Commands
- Build: `dotnet build -p:Platform=x64`
- Run: `dotnet run --framework net8.0-windows10.0.19041.0 -p:Platform=x64`
- Publish: `dotnet publish -c Release -p:Platform=x64`

Note: Specify `-p:Platform=x64` (or x86/ARM64) for dotnet CLI commands because the project targets multiple platforms.

## Key Instructions & Context
- Windows App SDK / WinUI 3 setup guidelines are in `.agent/skills.md`.
- Product architecture, requirements, and design docs are located under `documentation/`.
