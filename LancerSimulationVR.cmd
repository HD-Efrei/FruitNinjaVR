@echo off
setlocal
set "MetaRuntime=%LOCALAPPDATA%\MetaXRSimulator-Compatibility\78.1"
set "Game=%~dp0Build_Windows\My project.exe"

if not exist "%MetaRuntime%\meta_openxr_simulator.json" (
    echo Le runtime Meta XR compatible 78.1 n'est pas installe sur cet ordinateur.
    pause
    exit /b 1
)
if not exist "%Game%" (
    echo Build introuvable : %Game%
    pause
    exit /b 1
)

set "XR_RUNTIME_JSON=%MetaRuntime%\meta_openxr_simulator.json"
set "META_XRSIM_CONFIG_JSON=%MetaRuntime%\config\sim_core_configuration_compat.json"
start "" "%Game%" --fruitninja-vr=meta -screen-fullscreen 0
endlocal
