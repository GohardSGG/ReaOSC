@echo off
for %%f in (*.svg) do (
    inkscape "%%f" --export-area-drawing --export-height=46 --export-filename="%%~nf.png"
)